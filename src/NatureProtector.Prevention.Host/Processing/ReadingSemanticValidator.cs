using Microsoft.EntityFrameworkCore;
using NatureProtector.Infrastructure.Postgres.Persistence;
using NatureProtector.Shared.Contracts.Readings;
using NatureProtector.Shared.Messaging;

namespace NatureProtector.Prevention.Host.Processing;

public sealed class ReadingSemanticValidator(
    IDbContextFactory<NatureProtectorControlDbContext> dbContextFactory,
    ILogger<ReadingSemanticValidator> logger) : IReadingSemanticValidator
{
    public async Task<ReadingSemanticValidationResult> ValidateAsync(
        EventEnvelope<SensorReadingProducedPayload> envelope,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(envelope);

        await using var dbContext = await dbContextFactory.CreateDbContextAsync(cancellationToken);

        var sensor = await dbContext.SensorNodes
            .AsNoTracking()
            .Where(entity => entity.Id == envelope.Payload.SensorId)
            .Select(entity => new
            {
                entity.Id,
                entity.AreaId,
                entity.IsActive
            })
            .SingleOrDefaultAsync(cancellationToken);

        if (sensor is null)
        {
            logger.LogWarning(
                "Semantic validation failed because sensor was not found in control plane | EventId={EventId} | AreaId={AreaId} | SensorId={SensorId}",
                envelope.EventId,
                envelope.AreaId,
                envelope.Payload.SensorId);
            return ReadingSemanticValidationResult.Invalid(
                ReadingSemanticValidationReason.SensorNotFound,
                $"Sensor '{envelope.Payload.SensorId}' was not found in the control plane.");
        }

        if (!sensor.IsActive)
        {
            logger.LogWarning(
                "Semantic validation failed because sensor is inactive | EventId={EventId} | AreaId={AreaId} | SensorId={SensorId}",
                envelope.EventId,
                envelope.AreaId,
                envelope.Payload.SensorId);
            return ReadingSemanticValidationResult.Invalid(
                ReadingSemanticValidationReason.SensorInactive,
                $"Sensor '{envelope.Payload.SensorId}' is inactive in the control plane.");
        }

        if (sensor.AreaId != envelope.AreaId)
        {
            logger.LogWarning(
                "Semantic validation failed because envelope area does not match sensor deployment | EventId={EventId} | EnvelopeAreaId={EnvelopeAreaId} | SensorAreaId={SensorAreaId} | SensorId={SensorId}",
                envelope.EventId,
                envelope.AreaId,
                sensor.AreaId,
                envelope.Payload.SensorId);
            return ReadingSemanticValidationResult.Invalid(
                ReadingSemanticValidationReason.SensorAreaMismatch,
                $"Sensor '{envelope.Payload.SensorId}' belongs to area '{sensor.AreaId}', not '{envelope.AreaId}'.");
        }

        return ReadingSemanticValidationResult.Valid;
    }
}

public sealed class PassThroughReadingSemanticValidator : IReadingSemanticValidator
{
    public Task<ReadingSemanticValidationResult> ValidateAsync(
        EventEnvelope<SensorReadingProducedPayload> envelope,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(envelope);
        cancellationToken.ThrowIfCancellationRequested();
        return Task.FromResult(ReadingSemanticValidationResult.Valid);
    }
}
