using System.Diagnostics;
using System.Diagnostics.Metrics;
using Microsoft.Extensions.Options;
using NatureProtector.Prevention.Host.Configuration;
using NatureProtector.Shared.Observability;
using NatureProtector.Shared.Contracts.Readings;
using NatureProtector.Shared.Messaging;
using Npgsql;

namespace NatureProtector.Prevention.Host.Processing;

/*
 * Este serviço coordena o processamento transacional de um evento já aceite no
 * inbox.
 *
 * Rationale:
 * - O consumidor e o worker de retries precisam de reutilizar exatamente a
 *   mesma política de execução, retries e quarentena.
 * - Esta camada mantém a política de falhas separada da lógica de risco.
 *
 * Design considerations:
 * - O serviço marca o evento como concluído, reagendado ou em quarentena
 *   conforme o resultado do fluxo de processamento.
 * - A classificação de falhas é delegada para um componente próprio para
 *   permitir evolução futura da política.
 */

public sealed class ReadingEventProcessingService(
    ILogger<ReadingEventProcessingService> logger,
    IOptions<PreventionHostOptions> preventionHostOptions,
    ReadingRiskPipeline readingRiskPipeline,
    IReadingEventInbox readingEventInbox,
    IReadingSemanticValidator readingSemanticValidator,
    IProcessingFaultInjector processingFaultInjector,
    IProcessingFailureClassifier failureClassifier)
{
    private readonly PreventionHostOptions _options = preventionHostOptions.Value;

    /// <summary>
    /// Executa o processamento de um evento a partir do lease atribuído pelo
    /// inbox.
    /// </summary>
    public async Task ProcessAsync(
        EventEnvelope<SensorReadingProducedPayload> envelope,
        InboxProcessingLease lease,
        CancellationToken cancellationToken)
    {
        using var activity = PreventionHostTelemetry.ActivitySource.StartActivity("natureprotector.prevention.process");
        activity?.SetTag(TelemetryTags.EventId, envelope.EventId);
        activity?.SetTag(TelemetryTags.CorrelationId, envelope.CorrelationId);
        activity?.SetTag(TelemetryTags.AreaId, envelope.AreaId);
        activity?.SetTag(TelemetryTags.SensorId, envelope.Payload.SensorId);
        activity?.SetTag(TelemetryTags.InboxEventId, lease.InboxEventId);
        activity?.SetTag(TelemetryTags.AttemptNumber, lease.AttemptNumber);
        var processingStopwatch = Stopwatch.StartNew();

        try
        {
            var semanticValidation = await readingSemanticValidator.ValidateAsync(
                envelope,
                cancellationToken);

            if (!semanticValidation.IsValid)
            {
                processingStopwatch.Stop();
                var errorCode = semanticValidation.ReasonCode;
                var errorMessage = semanticValidation.Message ?? "Reading failed semantic validation.";

                await readingEventInbox.QuarantineProcessingAsync(
                    lease,
                    errorCode,
                    errorMessage,
                    errorCode,
                    errorMessage,
                    null,
                    cancellationToken);
                PreventionHostTelemetry.QuarantinedEvents.Add(1, new TagList
                {
                    { TelemetryTags.ErrorCode, errorCode },
                    { TelemetryTags.QuarantineCode, errorCode }
                });
                PreventionHostTelemetry.ProcessingDurationMs.Record(processingStopwatch.Elapsed.TotalMilliseconds, new TagList { { TelemetryTags.Outcome, "quarantined" } });

                logger.LogWarning(
                    "processing_total_ms={DurationMs} | EventId={EventId} | CorrelationId={CorrelationId} | InboxEventId={InboxEventId} | Attempt={AttemptNumber} | Outcome=quarantined | ReasonCode={ReasonCode}",
                    processingStopwatch.ElapsedMilliseconds,
                    envelope.EventId,
                    envelope.CorrelationId,
                    lease.InboxEventId,
                    lease.AttemptNumber,
                    errorCode);
                return;
            }

            await processingFaultInjector.InjectAsync(
                envelope,
                lease,
                cancellationToken);

            await readingRiskPipeline.ProcessAcceptedReadingAsync(
                envelope,
                cancellationToken);

            await readingEventInbox.CompleteProcessingAsync(
                lease,
                cancellationToken);

            processingStopwatch.Stop();
            PreventionHostTelemetry.ProcessedEvents.Add(1);
            PreventionHostTelemetry.ProcessingDurationMs.Record(processingStopwatch.Elapsed.TotalMilliseconds, new TagList { { TelemetryTags.Outcome, "completed" } });
            logger.LogInformation(
                "processing_total_ms={DurationMs} | EventId={EventId} | CorrelationId={CorrelationId} | InboxEventId={InboxEventId} | Attempt={AttemptNumber} | Outcome=completed",
                processingStopwatch.ElapsedMilliseconds,
                envelope.EventId,
                envelope.CorrelationId,
                lease.InboxEventId,
                lease.AttemptNumber);
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            processingStopwatch.Stop();
            PreventionHostTelemetry.ProcessingDurationMs.Record(processingStopwatch.Elapsed.TotalMilliseconds, new TagList { { TelemetryTags.Outcome, "cancelled" } });
            logger.LogWarning(
                "processing_total_ms={DurationMs} | EventId={EventId} | CorrelationId={CorrelationId} | InboxEventId={InboxEventId} | Attempt={AttemptNumber} | Outcome=cancelled",
                processingStopwatch.ElapsedMilliseconds,
                envelope.EventId,
                envelope.CorrelationId,
                lease.InboxEventId,
                lease.AttemptNumber);
        }
        catch (Exception ex)
        {
            processingStopwatch.Stop();
            var classification = failureClassifier.Classify(ex);

            if (ShouldRetry(lease.AttemptNumber, classification, out var retryDelay))
            {
                using var retryActivity = PreventionHostTelemetry.ActivitySource.StartActivity("natureprotector.prevention.retry.schedule");
                retryActivity?.SetTag(TelemetryTags.ErrorCode, classification.ErrorCode);
                retryActivity?.SetTag(TelemetryTags.RetryKind, classification.Kind);
                await readingEventInbox.ScheduleRetryAsync(
                    lease,
                    classification.ErrorCode,
                    ex.Message,
                    retryDelay,
                    cancellationToken);
                PreventionHostTelemetry.RetryScheduledEvents.Add(1, new TagList
                {
                    { TelemetryTags.ErrorCode, classification.ErrorCode },
                    { TelemetryTags.RetryKind, classification.Kind.ToString() }
                });
                PreventionHostTelemetry.ProcessingDurationMs.Record(processingStopwatch.Elapsed.TotalMilliseconds, new TagList { { TelemetryTags.Outcome, "retry_scheduled" } });

                logger.LogWarning(
                    ex,
                    "processing_total_ms={DurationMs} | EventId={EventId} | CorrelationId={CorrelationId} | InboxEventId={InboxEventId} | Attempt={AttemptNumber} | Outcome=retry_scheduled | DelaySeconds={DelaySeconds} | Kind={FailureKind}",
                    processingStopwatch.ElapsedMilliseconds,
                    envelope.EventId,
                    envelope.CorrelationId,
                    lease.InboxEventId,
                    lease.AttemptNumber,
                    retryDelay.TotalSeconds,
                    classification.Kind);

                return;
            }

            // Quando a política de novas tentativas já foi esgotada, o evento
            // deixa de circular no fluxo automático e passa para quarentena.
            var quarantineCode = classification.Kind == ProcessingFailureKind.Permanent
                ? "permanent_failure"
                : "retries_exhausted";
            var quarantineReason = classification.Kind == ProcessingFailureKind.Permanent
                ? "The event failed with a permanent error and was quarantined."
                : "The event exhausted the configured retry policy and was quarantined.";

            await readingEventInbox.QuarantineProcessingAsync(
                lease,
                classification.ErrorCode,
                BuildFailureMessage(ex),
                quarantineCode,
                quarantineReason,
                BuildFailureMetadataJson(ex),
                cancellationToken);
            PreventionHostTelemetry.QuarantinedEvents.Add(1, new TagList
            {
                { TelemetryTags.ErrorCode, classification.ErrorCode },
                { TelemetryTags.QuarantineCode, quarantineCode }
            });
            PreventionHostTelemetry.ProcessingDurationMs.Record(processingStopwatch.Elapsed.TotalMilliseconds, new TagList { { TelemetryTags.Outcome, "quarantined" } });

            logger.LogError(
                ex,
                "processing_total_ms={DurationMs} | EventId={EventId} | CorrelationId={CorrelationId} | InboxEventId={InboxEventId} | Attempt={AttemptNumber} | Outcome=quarantined | Kind={FailureKind}",
                processingStopwatch.ElapsedMilliseconds,
                envelope.EventId,
                envelope.CorrelationId,
                lease.InboxEventId,
                lease.AttemptNumber,
                classification.Kind);
        }
    }

    private static string BuildFailureMessage(Exception exception)
    {
        if (FindPostgresException(exception) is not { } postgresException)
        {
            return exception.Message;
        }

        return string.Join(" | ", new[]
        {
            exception.Message,
            $"SqlState={postgresException.SqlState}",
            $"MessageText={postgresException.MessageText}",
            string.IsNullOrWhiteSpace(postgresException.TableName) ? null : $"TableName={postgresException.TableName}",
            string.IsNullOrWhiteSpace(postgresException.ColumnName) ? null : $"ColumnName={postgresException.ColumnName}",
            string.IsNullOrWhiteSpace(postgresException.ConstraintName) ? null : $"ConstraintName={postgresException.ConstraintName}"
        }.Where(value => !string.IsNullOrWhiteSpace(value)));
    }

    private static string? BuildFailureMetadataJson(Exception exception)
    {
        if (FindPostgresException(exception) is not { } postgresException)
        {
            return null;
        }

        static string Escape(string value) => value
            .Replace("\\", "\\\\", StringComparison.Ordinal)
            .Replace("\"", "\\\"", StringComparison.Ordinal);

        var properties = new List<string>
        {
            $"\"sqlState\":\"{Escape(postgresException.SqlState)}\"",
            $"\"messageText\":\"{Escape(postgresException.MessageText)}\""
        };

        if (!string.IsNullOrWhiteSpace(postgresException.TableName))
        {
            properties.Add($"\"tableName\":\"{Escape(postgresException.TableName)}\"");
        }

        if (!string.IsNullOrWhiteSpace(postgresException.ColumnName))
        {
            properties.Add($"\"columnName\":\"{Escape(postgresException.ColumnName)}\"");
        }

        if (!string.IsNullOrWhiteSpace(postgresException.ConstraintName))
        {
            properties.Add($"\"constraintName\":\"{Escape(postgresException.ConstraintName)}\"");
        }

        return "{" + string.Join(",", properties) + "}";
    }

    private static PostgresException? FindPostgresException(Exception exception)
    {
        for (var current = exception; current is not null; current = current.InnerException)
        {
            if (current is PostgresException postgresException)
            {
                return postgresException;
            }
        }

        return null;
    }

    /// <summary>
    /// Determina se a tentativa atual ainda deve voltar à fila de retries.
    /// </summary>
    private bool ShouldRetry(
        int attemptNumber,
        ProcessingFailureClassification classification,
        out TimeSpan retryDelay)
    {
        retryDelay = TimeSpan.Zero;

        if (!classification.IsRetryable)
        {
            return false;
        }

        if (attemptNumber >= _options.MaxProcessingAttempts)
        {
            return false;
        }

        retryDelay = ResolveRetryDelay(attemptNumber);
        return true;
    }

    /// <summary>
    /// Resolve o atraso a aplicar antes da próxima tentativa.
    /// </summary>
    private TimeSpan ResolveRetryDelay(int attemptNumber)
    {
        if (_options.RetryDelaySeconds.Length == 0)
        {
            return TimeSpan.Zero;
        }

        var index = Math.Min(attemptNumber - 1, _options.RetryDelaySeconds.Length - 1);
        var seconds = Math.Max(0, _options.RetryDelaySeconds[index]);

        return TimeSpan.FromSeconds(seconds);
    }
}
