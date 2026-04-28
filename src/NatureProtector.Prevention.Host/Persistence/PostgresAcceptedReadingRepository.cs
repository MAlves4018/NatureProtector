using System.Text;
using System.Diagnostics;
using System.Diagnostics.Metrics;
using Microsoft.EntityFrameworkCore;
using NatureProtector.Infrastructure.Postgres.Persistence;
using NatureProtector.Infrastructure.Postgres.Projection;
using NatureProtector.Shared.Contracts.Readings;
using NatureProtector.Shared.Messaging;
using NatureProtector.Shared.Observability;

namespace NatureProtector.Prevention.Host.Persistence;

/*
 * Este repositório persiste em PostgreSQL o log de leituras aceites pela
 * pipeline.
 *
 * Rationale:
 * - As leituras aceites precisam de ficar rastreáveis mesmo depois de a
 *   pipeline continuar para avaliação de risco e projeções.
 * - Esta persistência também serve de base para inspeção manual e futuras
 *   análises históricas.
 *
 * Design considerations:
 * - A persistência é idempotente por EventId.
 * - O payload e o envelope completos ficam guardados em JSON para preservar o
 *   contrato recebido.
 */

public sealed class PostgresAcceptedReadingRepository(
    IDbContextFactory<NatureProtectorControlDbContext> dbContextFactory,
    ILogger<PostgresAcceptedReadingRepository> logger) : IAcceptedReadingRepository
{
    /// <summary>
    /// Persiste uma leitura aceite no log relacional.
    /// </summary>
    public async Task AddAsync(
        EventEnvelope<SensorReadingProducedPayload> envelope,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(envelope);
        using var activity = PreventionHostTelemetry.ActivitySource.StartActivity("natureprotector.prevention.postgres.write.accepted_reading");
        var stopwatch = Stopwatch.StartNew();

        await using var dbContext = await dbContextFactory.CreateDbContextAsync(cancellationToken);

        var exists = await dbContext.AcceptedReadingLogs
            .AsNoTracking()
            .AnyAsync(entity => entity.EventId == envelope.EventId, cancellationToken);

        if (exists)
        {
            return;
        }

        dbContext.AcceptedReadingLogs.Add(new AcceptedReadingLogRecord
        {
            Id = Guid.NewGuid(),
            EventId = envelope.EventId,
            AreaId = envelope.AreaId,
            SensorId = envelope.Payload.SensorId,
            MetricType = envelope.Payload.MetricType.ToString(),
            MeasurementUnit = envelope.Payload.Unit.ToString(),
            OperationalState = envelope.Payload.OperationalState.ToString(),
            Value = envelope.Payload.Value,
            EventTime = envelope.EventTime,
            IngestTime = envelope.IngestTime,
            Producer = envelope.Producer,
            CorrelationId = envelope.CorrelationId,
            PayloadJson = JsonEventSerializer.SerializeToString(envelope.Payload),
            EnvelopeJson = JsonEventSerializer.SerializeToString(envelope),
            CreatedAt = DateTimeOffset.UtcNow
        });

        await dbContext.SaveChangesAsync(cancellationToken);
        stopwatch.Stop();
        PreventionHostTelemetry.PostgresWriteDurationMs.Record(stopwatch.Elapsed.TotalMilliseconds, new TagList
        {
            { TelemetryTags.Operation, "accepted_reading" },
            { TelemetryTags.Outcome, "stored" }
        });

        logger.LogDebug(
            "Accepted reading persisted in PostgreSQL | EventId={EventId} | SensorId={SensorId}",
            envelope.EventId,
            envelope.Payload.SensorId);
    }

    /// <summary>
    /// Devolve todas as leituras aceites reconstruídas a partir do log.
    /// </summary>
    public async Task<IReadOnlyCollection<EventEnvelope<SensorReadingProducedPayload>>> GetAllAsync(
        CancellationToken cancellationToken)
    {
        await using var dbContext = await dbContextFactory.CreateDbContextAsync(cancellationToken);

        var rows = await dbContext.AcceptedReadingLogs
            .AsNoTracking()
            .ToListAsync(cancellationToken);

        return rows
            .OrderBy(entity => entity.EventTime)
            .Select(entity => JsonEventSerializer.Deserialize<EventEnvelope<SensorReadingProducedPayload>>(Encoding.UTF8.GetBytes(entity.EnvelopeJson)))
            .Where(entity => entity is not null)
            .Cast<EventEnvelope<SensorReadingProducedPayload>>()
            .ToArray();
    }
}
