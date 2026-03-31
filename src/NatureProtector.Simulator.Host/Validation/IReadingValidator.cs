using NatureProtector.Shared.Contracts.Readings;
using NatureProtector.Shared.Messaging;

/*
 * This interface defines the validation contract for incoming sensor-reading events.
 *
 * Rationale:
 * - Consumption and validation should remain separate concerns.
 * - This allows validation rules to evolve independently from RabbitMQ handling.
 */

namespace NatureProtector.Prevention.Host.Validation;

public interface IReadingValidator
{
    /// <summary>
    /// Validates one incoming sensor-reading event envelope.
    /// </summary>
    ReadingValidationResult Validate(EventEnvelope<SensorReadingProducedPayload>? envelope);
}