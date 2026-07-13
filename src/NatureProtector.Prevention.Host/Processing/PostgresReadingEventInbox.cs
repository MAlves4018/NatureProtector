using System.Diagnostics;
using System.Diagnostics.CodeAnalysis;
using System.Diagnostics.Metrics;
using System.Data;
using System.Text;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using NatureProtector.Infrastructure.Postgres.Persistence;
using NatureProtector.Infrastructure.Postgres.Pipeline;
using NatureProtector.Prevention.Host.Persistence;
using NatureProtector.Shared.Observability;
using NatureProtector.Shared.Contracts.Readings;
using NatureProtector.Shared.Messaging;

namespace NatureProtector.Prevention.Host.Processing;

/*
 * Este componente implementa o inbox durável do fluxo de prevenção em
 * PostgreSQL.
 *
 * Rationale:
 * - O fluxo operacional precisa de deduplicação, novas tentativas e quarentena sem depender do
 *   estado em memória do processo.
 * - O inbox separa a receção do broker do processamento efetivo, permitindo
 *   recuperação após falhas do host.
 *
 * Design considerations:
 * - O EventId é tratado como chave lógica de deduplicação.
 * - Cada tentativa de processamento fica registada separadamente para apoio a
 *   auditabilidade e diagnóstico.
 * - As novas tentativas e a quarentena atualizam o estado do evento e deixam
 *   rasto persistente no esquema da pipeline.
 */

public sealed class PostgresReadingEventInbox(
    IDbContextFactory<NatureProtectorControlDbContext> dbContextFactory,
    ILogger<PostgresReadingEventInbox> logger) : IReadingEventInbox
{
    private const string InvalidRetryPayloadCode = "invalid_retry_payload";
    private const string InvalidRetryPayloadReason = "Retry inbox event envelope could not be deserialized.";
    private const string ProcessingLeaseExpiredCode = "processing_lease_expired";
    private const string ProcessingLeaseExpiredReason = "Processing lease expired before the attempt completed.";

    /// <summary>
    /// Regista um evento recebido do broker e cria a primeira tentativa de
    /// processamento quando o evento ainda não existe no inbox.
    /// </summary>
    public async Task<InboxStoreResult> StoreIncomingAsync(
        EventEnvelope<SensorReadingProducedPayload> envelope,
        ReadOnlyMemory<byte> rawBody,
        string stage,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(envelope);
        using var activity = PreventionHostTelemetry.ActivitySource.StartActivity("natureprotector.prevention.inbox.store");
        activity?.SetTag(TelemetryTags.EventId, envelope.EventId);
        activity?.SetTag(TelemetryTags.CorrelationId, envelope.CorrelationId);
        activity?.SetTag(TelemetryTags.AreaId, envelope.AreaId);
        activity?.SetTag(TelemetryTags.Stage, stage);

        var storeIncomingStopwatch = Stopwatch.StartNew();
        await using var dbContext = await dbContextFactory.CreateDbContextAsync(cancellationToken);
        var now = DateTimeOffset.UtcNow;
        var envelopeJson = JsonEventSerializer.SerializeToString(envelope);
        var payloadJson = JsonEventSerializer.SerializeToString(envelope.Payload);

        var existing = await dbContext.InboxEvents
            .SingleOrDefaultAsync(entity => entity.EventId == envelope.EventId, cancellationToken);

        if (existing is not null)
        {
            return await BuildDuplicateStoreResultAsync(
                dbContext,
                existing,
                envelope,
                rawBody,
                stage,
                envelopeJson,
                storeIncomingStopwatch,
                cancellationToken);
        }

        var inboxEventId = Guid.NewGuid();
        var attemptId = Guid.NewGuid();
        var attemptNumber = 1;

        dbContext.InboxEvents.Add(new InboxEventRecord
        {
            Id = inboxEventId,
            EventId = envelope.EventId,
            SchemaVersion = envelope.SchemaVersion,
            CorrelationId = envelope.CorrelationId,
            Producer = envelope.Producer,
            EventType = envelope.EventType,
            AreaId = envelope.AreaId,
            EventTime = envelope.EventTime,
            ReceivedAt = now,
            IngestTime = envelope.IngestTime,
            PayloadJson = payloadJson,
            EnvelopeJson = envelopeJson,
            Status = InboxEventStatus.Processing,
            AttemptCount = attemptNumber,
            LastAttemptAt = now
        });

        dbContext.ProcessingAttempts.Add(new ProcessingAttemptRecord
        {
            Id = attemptId,
            InboxEventId = inboxEventId,
            AttemptNumber = attemptNumber,
            Stage = stage,
            StartedAt = now,
            Outcome = ProcessingAttemptOutcome.Started
        });

        try
        {
            await dbContext.SaveChangesAsync(cancellationToken);
        }
        catch (DbUpdateException ex) when (ExpectedUniqueViolationDetector.IsExpected(ex, NatureProtectorUniqueConstraints.InboxEventId))
        {
            await using var duplicateContext = await dbContextFactory.CreateDbContextAsync(cancellationToken);
            existing = await duplicateContext.InboxEvents
                .SingleAsync(entity => entity.EventId == envelope.EventId, cancellationToken);

            return await BuildDuplicateStoreResultAsync(
                duplicateContext,
                existing,
                envelope,
                rawBody,
                stage,
                envelopeJson,
                storeIncomingStopwatch,
                cancellationToken);
        }

        storeIncomingStopwatch.Stop();

        logger.LogInformation(
            "inbox_store_ms={DurationMs} | EventId={EventId} | CorrelationId={CorrelationId} | InboxEventId={InboxEventId} | Attempt={AttemptNumber} | Outcome=stored",
            storeIncomingStopwatch.ElapsedMilliseconds,
            envelope.EventId,
            envelope.CorrelationId,
            inboxEventId,
            attemptNumber);
        PreventionHostTelemetry.InboxStoreDurationMs.Record(storeIncomingStopwatch.Elapsed.TotalMilliseconds, new TagList { { TelemetryTags.Outcome, "stored" } });

        return new InboxStoreResult(
            inboxEventId,
            InboxEventStatus.Processing,
            false,
            true,
            new InboxProcessingLease(inboxEventId, attemptId, attemptNumber, stage));
    }

    /// <summary>
    /// Regista uma mensagem rejeitada antes de ela entrar no fluxo de processamento.
    /// </summary>
    public async Task StoreRejectedAsync(
        ReadOnlyMemory<byte> rawBody,
        string rejectionCode,
        string rejectionReason,
        RejectedEventMetadata? metadata,
        CancellationToken cancellationToken)
    {
        await using var dbContext = await dbContextFactory.CreateDbContextAsync(cancellationToken);

        dbContext.RejectedEvents.Add(new RejectedEventRecord
        {
            Id = Guid.NewGuid(),
            RejectionCode = rejectionCode,
            RejectionReason = rejectionReason,
            RejectedAt = DateTimeOffset.UtcNow,
            RawBodyUtf8 = Encoding.UTF8.GetString(rawBody.Span),
            MetadataJson = metadata is null ? null : JsonSerializer.Serialize(metadata)
        });

        await dbContext.SaveChangesAsync(cancellationToken);
    }

    /// <summary>
    /// Marca um evento e a respetiva tentativa como concluídos com sucesso.
    /// </summary>
    public async Task CompleteProcessingAsync(InboxProcessingLease lease, CancellationToken cancellationToken)
    {
        await using var dbContext = await dbContextFactory.CreateDbContextAsync(cancellationToken);
        await using var transaction = await dbContext.Database.BeginTransactionAsync(IsolationLevel.ReadCommitted, cancellationToken);
        var now = DateTimeOffset.UtcNow;
        var inboxRows = await dbContext.InboxEvents
            .Where(entity => entity.Id == lease.InboxEventId && entity.Status == InboxEventStatus.Processing && entity.AttemptCount == lease.AttemptNumber)
            .ExecuteUpdateAsync(setters => setters
                .SetProperty(entity => entity.Status, InboxEventStatus.Processed)
                .SetProperty(entity => entity.LastAttemptAt, now)
                .SetProperty(entity => entity.LastProcessedAt, now)
                .SetProperty(entity => entity.NextAttemptNotBefore, (DateTimeOffset?)null)
                .SetProperty(entity => entity.QuarantinedAt, (DateTimeOffset?)null)
                .SetProperty(entity => entity.LastErrorCode, (string?)null)
                .SetProperty(entity => entity.LastErrorMessage, (string?)null), cancellationToken);
        var attemptRows = inboxRows == 1
            ? await dbContext.ProcessingAttempts
                .Where(entity => entity.Id == lease.AttemptId && entity.InboxEventId == lease.InboxEventId && entity.AttemptNumber == lease.AttemptNumber && entity.Outcome == ProcessingAttemptOutcome.Started)
                .ExecuteUpdateAsync(setters => setters
                    .SetProperty(entity => entity.FinishedAt, now)
                    .SetProperty(entity => entity.Outcome, ProcessingAttemptOutcome.Succeeded)
                    .SetProperty(entity => entity.ErrorCode, (string?)null)
                    .SetProperty(entity => entity.ErrorMessage, (string?)null), cancellationToken)
            : 0;
        if (inboxRows != 1 || attemptRows != 1)
        {
            await transaction.RollbackAsync(cancellationToken);
            return;
        }
        await transaction.CommitAsync(cancellationToken);
    }

    /// <summary>
    /// Agenda nova tentativa para um evento que falhou com erro recuperável.
    /// </summary>
    public async Task ScheduleRetryAsync(
        InboxProcessingLease lease,
        string errorCode,
        string errorMessage,
        TimeSpan retryDelay,
        CancellationToken cancellationToken)
    {
        using var activity = PreventionHostTelemetry.ActivitySource.StartActivity("natureprotector.prevention.inbox.retry.schedule");
        activity?.SetTag(TelemetryTags.InboxEventId, lease.InboxEventId);
        activity?.SetTag(TelemetryTags.AttemptNumber, lease.AttemptNumber);
        activity?.SetTag(TelemetryTags.ErrorCode, errorCode);
        await using var dbContext = await dbContextFactory.CreateDbContextAsync(cancellationToken);
        await using var transaction = await dbContext.Database.BeginTransactionAsync(IsolationLevel.ReadCommitted, cancellationToken);
        var now = DateTimeOffset.UtcNow;
        var code = Truncate(errorCode, 100);
        var message = Truncate(errorMessage, 2000);
        var inboxRows = await dbContext.InboxEvents
            .Where(entity => entity.Id == lease.InboxEventId && entity.Status == InboxEventStatus.Processing && entity.AttemptCount == lease.AttemptNumber)
            .ExecuteUpdateAsync(setters => setters
                .SetProperty(entity => entity.Status, InboxEventStatus.RetryPending)
                .SetProperty(entity => entity.LastAttemptAt, now)
                .SetProperty(entity => entity.LastProcessedAt, (DateTimeOffset?)null)
                .SetProperty(entity => entity.NextAttemptNotBefore, now.Add(retryDelay))
                .SetProperty(entity => entity.QuarantinedAt, (DateTimeOffset?)null)
                .SetProperty(entity => entity.LastErrorCode, code)
                .SetProperty(entity => entity.LastErrorMessage, message), cancellationToken);
        var attemptRows = inboxRows == 1
            ? await dbContext.ProcessingAttempts
                .Where(entity => entity.Id == lease.AttemptId && entity.InboxEventId == lease.InboxEventId && entity.AttemptNumber == lease.AttemptNumber && entity.Outcome == ProcessingAttemptOutcome.Started)
                .ExecuteUpdateAsync(setters => setters
                    .SetProperty(entity => entity.FinishedAt, now)
                    .SetProperty(entity => entity.Outcome, ProcessingAttemptOutcome.RetryScheduled)
                    .SetProperty(entity => entity.ErrorCode, code)
                    .SetProperty(entity => entity.ErrorMessage, message), cancellationToken)
            : 0;
        if (inboxRows != 1 || attemptRows != 1)
        {
            await transaction.RollbackAsync(cancellationToken);
            return;
        }
        await transaction.CommitAsync(cancellationToken);
    }

    /// <summary>
    /// Procura o próximo evento em retry cujo atraso já expirou e inicia nova
    /// tentativa de processamento.
    /// </summary>
    public async Task<InboxRetryWorkItem?> TryStartDueRetryAsync(
        string stage,
        CancellationToken cancellationToken,
        TimeSpan? processingLeaseTimeout = null,
        int? maxProcessingAttempts = null)
    {
        while (true)
        {
            await using var dbContext = await dbContextFactory.CreateDbContextAsync(cancellationToken);
            var now = DateTimeOffset.UtcNow;
            var recoverStaleProcessing = processingLeaseTimeout is { } timeout && timeout > TimeSpan.Zero;
            var staleProcessingCutoff = recoverStaleProcessing
                ? now.Subtract(processingLeaseTimeout!.Value)
                : DateTimeOffset.MinValue;

            var inboxEvent = await dbContext.InboxEvents
                .Where(entity =>
                    (entity.Status == InboxEventStatus.RetryPending &&
                     entity.NextAttemptNotBefore != null &&
                     entity.NextAttemptNotBefore <= now) ||
                    (recoverStaleProcessing &&
                     entity.Status == InboxEventStatus.Processing &&
                     entity.LastAttemptAt != null &&
                     entity.LastAttemptAt <= staleProcessingCutoff))
                .OrderBy(entity => entity.Status == InboxEventStatus.RetryPending ? 0 : 1)
                .ThenBy(entity => entity.NextAttemptNotBefore ?? entity.LastAttemptAt ?? entity.ReceivedAt)
                .FirstOrDefaultAsync(cancellationToken);

            if (inboxEvent is null)
            {
                return null;
            }

            var isRecoveringStaleProcessing = inboxEvent.Status == InboxEventStatus.Processing;
            var attemptNumber = inboxEvent.AttemptCount + 1;
            var attemptId = Guid.NewGuid();

            if (isRecoveringStaleProcessing &&
                maxProcessingAttempts.HasValue &&
                inboxEvent.AttemptCount >= maxProcessingAttempts.Value)
            {
                await QuarantineExpiredProcessingLeaseAsync(
                    dbContext,
                    inboxEvent,
                    stage,
                    now,
                    cancellationToken);

                logger.LogWarning(
                    "Quarantined stale processing inbox event after lease expiry. InboxEventId={InboxEventId} EventId={EventId} Attempt={AttemptNumber}",
                    inboxEvent.Id,
                    inboxEvent.EventId,
                    inboxEvent.AttemptCount);

                continue;
            }

            if (!TryDeserializeEnvelope(inboxEvent.EnvelopeJson, out var envelope, out var errorMessage))
            {
                await QuarantineMalformedRetryAsync(
                    dbContext,
                    inboxEvent,
                    stage,
                    now,
                    attemptId,
                    attemptNumber,
                    errorMessage,
                    cancellationToken);

                logger.LogWarning(
                    "Quarantined malformed retry inbox event. InboxEventId={InboxEventId} EventId={EventId} Attempt={AttemptNumber} Error={Error}",
                    inboxEvent.Id,
                    inboxEvent.EventId,
                    attemptNumber,
                    errorMessage);

                continue;
            }

            if (isRecoveringStaleProcessing)
            {
                var expiredAttempt = await dbContext.ProcessingAttempts
                    .SingleOrDefaultAsync(
                        entity =>
                            entity.InboxEventId == inboxEvent.Id &&
                            entity.AttemptNumber == inboxEvent.AttemptCount,
                        cancellationToken);

                if (expiredAttempt is not null && expiredAttempt.Outcome == ProcessingAttemptOutcome.Started)
                {
                    expiredAttempt.FinishedAt = now;
                    expiredAttempt.Outcome = ProcessingAttemptOutcome.RetryScheduled;
                    expiredAttempt.ErrorCode = ProcessingLeaseExpiredCode;
                    expiredAttempt.ErrorMessage = ProcessingLeaseExpiredReason;
                }
            }

            // A mudança para Processing e a criação da nova tentativa ficam na
            // mesma transação para evitar leases ambíguos.
            inboxEvent.Status = InboxEventStatus.Processing;
            inboxEvent.AttemptCount = attemptNumber;
            inboxEvent.LastAttemptAt = now;
            inboxEvent.NextAttemptNotBefore = null;

            dbContext.ProcessingAttempts.Add(new ProcessingAttemptRecord
            {
                Id = attemptId,
                InboxEventId = inboxEvent.Id,
                AttemptNumber = attemptNumber,
                Stage = stage,
                StartedAt = now,
                Outcome = ProcessingAttemptOutcome.Started
            });

            try
            {
                await dbContext.SaveChangesAsync(cancellationToken);
            }
            catch (DbUpdateException ex) when (ExpectedUniqueViolationDetector.IsExpected(ex, NatureProtectorUniqueConstraints.ProcessingAttemptNumber))
            {
                continue;
            }

            if (isRecoveringStaleProcessing)
            {
                logger.LogWarning(
                    "Recovered stale processing inbox event after lease expiry. InboxEventId={InboxEventId} EventId={EventId} Attempt={AttemptNumber}",
                    inboxEvent.Id,
                    inboxEvent.EventId,
                    attemptNumber);
            }

            return new InboxRetryWorkItem(
                envelope,
                new InboxProcessingLease(inboxEvent.Id, attemptId, attemptNumber, stage));
        }
    }

    /// <summary>
    /// Move o evento para quarentena depois de falha permanente ou retries
    /// esgotados.
    /// </summary>
    public async Task QuarantineProcessingAsync(
        InboxProcessingLease lease,
        string errorCode,
        string errorMessage,
        string quarantineCode,
        string quarantineReason,
        string? errorMetadataJson,
        CancellationToken cancellationToken)
    {
        using var activity = PreventionHostTelemetry.ActivitySource.StartActivity("natureprotector.prevention.inbox.quarantine");
        activity?.SetTag(TelemetryTags.InboxEventId, lease.InboxEventId);
        activity?.SetTag(TelemetryTags.AttemptNumber, lease.AttemptNumber);
        activity?.SetTag(TelemetryTags.ErrorCode, errorCode);
        activity?.SetTag(TelemetryTags.QuarantineCode, quarantineCode);
        await using var dbContext = await dbContextFactory.CreateDbContextAsync(cancellationToken);
        var now = DateTimeOffset.UtcNow;

        var inboxEvent = await dbContext.InboxEvents
            .SingleAsync(entity => entity.Id == lease.InboxEventId, cancellationToken);
        var attempt = await dbContext.ProcessingAttempts
            .SingleAsync(entity => entity.Id == lease.AttemptId, cancellationToken);

        if (!IsCurrentStartedLease(inboxEvent, attempt, lease))
        {
            LogIgnoredStaleLease("quarantine", inboxEvent, attempt, lease);
            return;
        }

        inboxEvent.Status = InboxEventStatus.Quarantined;
        inboxEvent.LastAttemptAt = now;
        inboxEvent.LastProcessedAt = null;
        inboxEvent.NextAttemptNotBefore = null;
        inboxEvent.QuarantinedAt = now;
        inboxEvent.LastErrorCode = Truncate(errorCode, 100);
        inboxEvent.LastErrorMessage = Truncate(errorMessage, 2000);

        attempt.FinishedAt = now;
        attempt.Outcome = ProcessingAttemptOutcome.Quarantined;
        attempt.ErrorCode = Truncate(errorCode, 100);
        attempt.ErrorMessage = Truncate(errorMessage, 2000);

        dbContext.QuarantinedEvents.Add(new QuarantinedEventRecord
        {
            Id = Guid.NewGuid(),
            InboxEventId = inboxEvent.Id,
            EventId = inboxEvent.EventId,
            FinalAttemptNumber = lease.AttemptNumber,
            QuarantineCode = Truncate(quarantineCode, 100),
            QuarantineReason = Truncate(quarantineReason, 2000),
            QuarantinedAt = now,
            MetadataJson = MergeQuarantineMetadata(lease.Stage, errorMetadataJson)
        });

        await dbContext.SaveChangesAsync(cancellationToken);
    }

    private void LogIgnoredStaleLease(
        string operation,
        InboxEventRecord inboxEvent,
        ProcessingAttemptRecord attempt,
        InboxProcessingLease lease)
    {
        logger.LogWarning(
            "Ignored stale inbox lease finalization. Operation={Operation} InboxEventId={InboxEventId} LeaseAttempt={LeaseAttemptNumber} CurrentAttempt={CurrentAttemptCount} Status={Status} AttemptOutcome={AttemptOutcome}",
            operation,
            lease.InboxEventId,
            lease.AttemptNumber,
            inboxEvent.AttemptCount,
            inboxEvent.Status,
            attempt.Outcome);
    }

    private static bool IsCurrentStartedLease(
        InboxEventRecord inboxEvent,
        ProcessingAttemptRecord attempt,
        InboxProcessingLease lease)
    {
        return inboxEvent.Status == InboxEventStatus.Processing &&
            inboxEvent.AttemptCount == lease.AttemptNumber &&
            attempt.InboxEventId == lease.InboxEventId &&
            attempt.AttemptNumber == lease.AttemptNumber &&
            attempt.Outcome == ProcessingAttemptOutcome.Started;
    }

    private static string MergeQuarantineMetadata(string stage, string? errorMetadataJson)
    {
        static string Escape(string value) => value
            .Replace("\\", "\\\\", StringComparison.Ordinal)
            .Replace("\"", "\\\"", StringComparison.Ordinal);

        if (string.IsNullOrWhiteSpace(errorMetadataJson) || errorMetadataJson.Trim() == "{}")
        {
            return $"{{\"stage\":\"{Escape(stage)}\"}}";
        }

        var trimmed = errorMetadataJson.Trim();
        if (trimmed.StartsWith('{') && trimmed.EndsWith('}'))
        {
            var inner = trimmed[1..^1].Trim();
            return string.IsNullOrWhiteSpace(inner)
                ? $"{{\"stage\":\"{Escape(stage)}\"}}"
                : $"{{\"stage\":\"{Escape(stage)}\",{inner}}}";
        }

        return $"{{\"stage\":\"{Escape(stage)}\",\"errorMetadata\":\"{Escape(errorMetadataJson)}\"}}";
    }

    /// <summary>
    /// Limita o tamanho das mensagens persistidas para respeitar os tamanhos do
    /// esquema relacional.
    /// </summary>
    private static string Truncate(string value, int maxLength)
    {
        if (string.IsNullOrEmpty(value))
        {
            return value;
        }

        return value.Length <= maxLength
            ? value
            : value[..maxLength];
    }

    /// <summary>
    /// Quarentena um evento de retry cujo envelope persistido já não pode ser
    /// desserializado.
    /// </summary>
    private static async Task QuarantineMalformedRetryAsync(
        NatureProtectorControlDbContext dbContext,
        InboxEventRecord inboxEvent,
        string stage,
        DateTimeOffset now,
        Guid attemptId,
        int attemptNumber,
        string errorMessage,
        CancellationToken cancellationToken)
    {
        inboxEvent.Status = InboxEventStatus.Quarantined;
        inboxEvent.AttemptCount = attemptNumber;
        inboxEvent.LastAttemptAt = now;
        inboxEvent.LastProcessedAt = null;
        inboxEvent.NextAttemptNotBefore = null;
        inboxEvent.QuarantinedAt = now;
        inboxEvent.LastErrorCode = InvalidRetryPayloadCode;
        inboxEvent.LastErrorMessage = Truncate(errorMessage, 2000);

        dbContext.ProcessingAttempts.Add(new ProcessingAttemptRecord
        {
            Id = attemptId,
            InboxEventId = inboxEvent.Id,
            AttemptNumber = attemptNumber,
            Stage = stage,
            StartedAt = now,
            FinishedAt = now,
            Outcome = ProcessingAttemptOutcome.Quarantined,
            ErrorCode = InvalidRetryPayloadCode,
            ErrorMessage = Truncate(errorMessage, 2000)
        });

        dbContext.QuarantinedEvents.Add(new QuarantinedEventRecord
        {
            Id = Guid.NewGuid(),
            InboxEventId = inboxEvent.Id,
            EventId = inboxEvent.EventId,
            FinalAttemptNumber = attemptNumber,
            QuarantineCode = InvalidRetryPayloadCode,
            QuarantineReason = InvalidRetryPayloadReason,
            QuarantinedAt = now,
            MetadataJson = $"{{\"stage\":\"{stage}\"}}"
        });

        await dbContext.SaveChangesAsync(cancellationToken);
    }

    private static async Task QuarantineExpiredProcessingLeaseAsync(
        NatureProtectorControlDbContext dbContext,
        InboxEventRecord inboxEvent,
        string stage,
        DateTimeOffset now,
        CancellationToken cancellationToken)
    {
        inboxEvent.Status = InboxEventStatus.Quarantined;
        inboxEvent.LastProcessedAt = null;
        inboxEvent.NextAttemptNotBefore = null;
        inboxEvent.QuarantinedAt = now;
        inboxEvent.LastErrorCode = ProcessingLeaseExpiredCode;
        inboxEvent.LastErrorMessage = ProcessingLeaseExpiredReason;

        var attempt = await dbContext.ProcessingAttempts
            .SingleOrDefaultAsync(
                entity =>
                    entity.InboxEventId == inboxEvent.Id &&
                    entity.AttemptNumber == inboxEvent.AttemptCount,
                cancellationToken);

        if (attempt is not null && attempt.Outcome == ProcessingAttemptOutcome.Started)
        {
            attempt.FinishedAt = now;
            attempt.Outcome = ProcessingAttemptOutcome.Quarantined;
            attempt.ErrorCode = ProcessingLeaseExpiredCode;
            attempt.ErrorMessage = ProcessingLeaseExpiredReason;
        }

        dbContext.QuarantinedEvents.Add(new QuarantinedEventRecord
        {
            Id = Guid.NewGuid(),
            InboxEventId = inboxEvent.Id,
            EventId = inboxEvent.EventId,
            FinalAttemptNumber = inboxEvent.AttemptCount,
            QuarantineCode = ProcessingLeaseExpiredCode,
            QuarantineReason = ProcessingLeaseExpiredReason,
            QuarantinedAt = now,
            MetadataJson = $"{{\"stage\":\"{stage}\"}}"
        });

        await dbContext.SaveChangesAsync(cancellationToken);
    }

    /// <summary>
    /// Tenta reconstruir o envelope original a partir do JSON guardado no inbox.
    /// </summary>
    private static bool TryDeserializeEnvelope(
        string envelopeJson,
        [NotNullWhen(true)] out EventEnvelope<SensorReadingProducedPayload>? envelope,
        out string errorMessage)
    {
        try
        {
            envelope = JsonEventSerializer.Deserialize<EventEnvelope<SensorReadingProducedPayload>>(
                Encoding.UTF8.GetBytes(envelopeJson));

            if (envelope is null)
            {
                errorMessage = "Retry inbox event contains a null envelope.";
                return false;
            }

            if (envelope.Payload is null)
            {
                errorMessage = "Retry inbox event contains a null payload.";
                return false;
            }

            errorMessage = string.Empty;
            return true;
        }
        catch (Exception ex)
        {
            envelope = null;
            errorMessage = $"{InvalidRetryPayloadReason} {ex.Message}".Trim();
            return false;
        }
    }

    private async Task<InboxStoreResult> BuildDuplicateStoreResultAsync(
        NatureProtectorControlDbContext dbContext,
        InboxEventRecord existing,
        EventEnvelope<SensorReadingProducedPayload> envelope,
        ReadOnlyMemory<byte> rawBody,
        string stage,
        string envelopeJson,
        Stopwatch storeIncomingStopwatch,
        CancellationToken cancellationToken)
    {
        if (!string.Equals(existing.EnvelopeJson, envelopeJson, StringComparison.Ordinal))
        {
            dbContext.RejectedEvents.Add(new RejectedEventRecord
            {
                Id = Guid.NewGuid(),
                InboxEventId = existing.Id,
                EventId = envelope.EventId,
                RejectionCode = "duplicate_payload_mismatch",
                RejectionReason = "Received a duplicate event id with a different payload.",
                RejectedAt = DateTimeOffset.UtcNow,
                RawBodyUtf8 = Encoding.UTF8.GetString(rawBody.Span),
                MetadataJson = $"{{\"stage\":\"{stage}\"}}"
            });

            await dbContext.SaveChangesAsync(cancellationToken);
        }

        storeIncomingStopwatch.Stop();
        logger.LogInformation(
            "inbox_store_ms={DurationMs} | EventId={EventId} | CorrelationId={CorrelationId} | InboxEventId={InboxEventId} | Status={Status} | Outcome=duplicate",
            storeIncomingStopwatch.ElapsedMilliseconds,
            envelope.EventId,
            envelope.CorrelationId,
            existing.Id,
            existing.Status);
        PreventionHostTelemetry.InboxStoreDurationMs.Record(storeIncomingStopwatch.Elapsed.TotalMilliseconds, new TagList { { TelemetryTags.Outcome, "duplicate" } });

        return new InboxStoreResult(
            existing.Id,
            existing.Status,
            true,
            false,
            null);
    }
}
