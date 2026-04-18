using System.Diagnostics;
using System.Diagnostics.CodeAnalysis;
using System.Text;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using NatureProtector.Infrastructure.Postgres.Persistence;
using NatureProtector.Infrastructure.Postgres.Pipeline;
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

        var storeIncomingStopwatch = Stopwatch.StartNew();
        await using var dbContext = await dbContextFactory.CreateDbContextAsync(cancellationToken);
        var now = DateTimeOffset.UtcNow;
        var envelopeJson = JsonEventSerializer.SerializeToString(envelope);
        var payloadJson = JsonEventSerializer.SerializeToString(envelope.Payload);

        var existing = await dbContext.InboxEvents
            .SingleOrDefaultAsync(entity => entity.EventId == envelope.EventId, cancellationToken);

        if (existing is not null)
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
                    RejectedAt = now,
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

            return new InboxStoreResult(
                existing.Id,
                existing.Status,
                true,
                false,
                null);
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

        await dbContext.SaveChangesAsync(cancellationToken);
        storeIncomingStopwatch.Stop();

        logger.LogInformation(
            "inbox_store_ms={DurationMs} | EventId={EventId} | CorrelationId={CorrelationId} | InboxEventId={InboxEventId} | Attempt={AttemptNumber} | Outcome=stored",
            storeIncomingStopwatch.ElapsedMilliseconds,
            envelope.EventId,
            envelope.CorrelationId,
            inboxEventId,
            attemptNumber);

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
        var now = DateTimeOffset.UtcNow;

        var inboxEvent = await dbContext.InboxEvents
            .SingleAsync(entity => entity.Id == lease.InboxEventId, cancellationToken);
        var attempt = await dbContext.ProcessingAttempts
            .SingleAsync(entity => entity.Id == lease.AttemptId, cancellationToken);

        inboxEvent.Status = InboxEventStatus.Processed;
        inboxEvent.LastAttemptAt = now;
        inboxEvent.LastProcessedAt = now;
        inboxEvent.NextAttemptNotBefore = null;
        inboxEvent.QuarantinedAt = null;
        inboxEvent.LastErrorCode = null;
        inboxEvent.LastErrorMessage = null;

        attempt.FinishedAt = now;
        attempt.Outcome = ProcessingAttemptOutcome.Succeeded;
        attempt.ErrorCode = null;
        attempt.ErrorMessage = null;

        await dbContext.SaveChangesAsync(cancellationToken);
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
        await using var dbContext = await dbContextFactory.CreateDbContextAsync(cancellationToken);
        var now = DateTimeOffset.UtcNow;

        var inboxEvent = await dbContext.InboxEvents
            .SingleAsync(entity => entity.Id == lease.InboxEventId, cancellationToken);
        var attempt = await dbContext.ProcessingAttempts
            .SingleAsync(entity => entity.Id == lease.AttemptId, cancellationToken);

        inboxEvent.Status = InboxEventStatus.RetryPending;
        inboxEvent.LastAttemptAt = now;
        inboxEvent.LastProcessedAt = null;
        inboxEvent.NextAttemptNotBefore = now.Add(retryDelay);
        inboxEvent.QuarantinedAt = null;
        inboxEvent.LastErrorCode = Truncate(errorCode, 100);
        inboxEvent.LastErrorMessage = Truncate(errorMessage, 2000);

        attempt.FinishedAt = now;
        attempt.Outcome = ProcessingAttemptOutcome.RetryScheduled;
        attempt.ErrorCode = Truncate(errorCode, 100);
        attempt.ErrorMessage = Truncate(errorMessage, 2000);

        await dbContext.SaveChangesAsync(cancellationToken);
    }

    /// <summary>
    /// Procura o próximo evento em retry cujo atraso já expirou e inicia nova
    /// tentativa de processamento.
    /// </summary>
    public async Task<InboxRetryWorkItem?> TryStartDueRetryAsync(
        string stage,
        CancellationToken cancellationToken)
    {
        while (true)
        {
            await using var dbContext = await dbContextFactory.CreateDbContextAsync(cancellationToken);
            var now = DateTimeOffset.UtcNow;

            var inboxEvent = await dbContext.InboxEvents
                .Where(entity =>
                    entity.Status == InboxEventStatus.RetryPending &&
                    entity.NextAttemptNotBefore != null &&
                    entity.NextAttemptNotBefore <= now)
                .OrderBy(entity => entity.NextAttemptNotBefore)
                .FirstOrDefaultAsync(cancellationToken);

            if (inboxEvent is null)
            {
                return null;
            }

            var attemptNumber = inboxEvent.AttemptCount + 1;
            var attemptId = Guid.NewGuid();

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

            await dbContext.SaveChangesAsync(cancellationToken);

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
        CancellationToken cancellationToken)
    {
        await using var dbContext = await dbContextFactory.CreateDbContextAsync(cancellationToken);
        var now = DateTimeOffset.UtcNow;

        var inboxEvent = await dbContext.InboxEvents
            .SingleAsync(entity => entity.Id == lease.InboxEventId, cancellationToken);
        var attempt = await dbContext.ProcessingAttempts
            .SingleAsync(entity => entity.Id == lease.AttemptId, cancellationToken);

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
            MetadataJson = $"{{\"stage\":\"{lease.Stage}\"}}"
        });

        await dbContext.SaveChangesAsync(cancellationToken);
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
}
