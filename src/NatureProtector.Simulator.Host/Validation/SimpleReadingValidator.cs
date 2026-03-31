using NatureProtector.Shared.Contracts.Readings;
using NatureProtector.Shared.Messaging;

/*
 * This class performs the Day 5 validation rules for incoming readings.
 *
 * Rationale:
 * - The Prevention.Host needs a lightweight acceptance gate before persisting data.
 * - At this stage we only validate structural correctness and basic plausibility.
 *
 * Design considerations:
 * - Business rejections are returned as validation results instead of exceptions.
 * - Only nominal readings are accepted for persistence in this phase.
 * - Supported metric and unit combinations are restricted to what the current
 *   simulator publishes reliably.
 */

namespace NatureProtector.Prevention.Host.Validation;

public sealed class SimpleReadingValidator : IReadingValidator
{
    public ReadingValidationResult Validate(EventEnvelope<SensorReadingProducedPayload>? envelope)
    {
        if (envelope is null)
        {
            return ReadingValidationResult.Reject("Envelope is null.");
        }

        if (envelope.EventId == Guid.Empty)
        {
            return ReadingValidationResult.Reject("EventId must not be empty.");
        }

        if (string.IsNullOrWhiteSpace(envelope.CorrelationId))
        {
            return ReadingValidationResult.Reject("CorrelationId must not be empty.");
        }

        if (envelope.AreaId == Guid.Empty)
        {
            return ReadingValidationResult.Reject("AreaId must not be empty.");
        }

        if (!string.Equals(
                envelope.EventType,
                EventTypes.SensorReadingProduced,
                StringComparison.Ordinal))
        {
            return ReadingValidationResult.Reject(
                $"Unsupported event type '{envelope.EventType}'.");
        }

        if (envelope.Payload is null)
        {
            return ReadingValidationResult.Reject("Payload must not be null.");
        }

        var payload = envelope.Payload;

        if (payload.SimulationRunId == Guid.Empty)
        {
            return ReadingValidationResult.Reject("SimulationRunId must not be empty.");
        }

        if (payload.SensorId == Guid.Empty)
        {
            return ReadingValidationResult.Reject("SensorId must not be empty.");
        }

        if (string.IsNullOrWhiteSpace(payload.SensorName))
        {
            return ReadingValidationResult.Reject("SensorName must not be empty.");
        }

        if (double.IsNaN(payload.Value) || double.IsInfinity(payload.Value))
        {
            return ReadingValidationResult.Reject("Value must be a finite number.");
        }

        if (payload.Latitude is < -90.0 or > 90.0)
        {
            return ReadingValidationResult.Reject(
                $"Latitude '{payload.Latitude}' is outside the valid range.");
        }

        if (payload.Longitude is < -180.0 or > 180.0)
        {
            return ReadingValidationResult.Reject(
                $"Longitude '{payload.Longitude}' is outside the valid range.");
        }

        if (payload.OperationalState != SensorOperationalState.Nominal)
        {
            return ReadingValidationResult.Reject(
                $"Operational state '{payload.OperationalState}' is not accepted.");
        }

        if (!MetricAndUnitMatch(payload.MetricType, payload.Unit))
        {
            return ReadingValidationResult.Reject(
                $"Metric '{payload.MetricType}' is incompatible with unit '{payload.Unit}'.");
        }

        if (!ValueIsPlausible(payload.MetricType, payload.Value))
        {
            return ReadingValidationResult.Reject(
                $"Value '{payload.Value}' is outside the accepted range for metric '{payload.MetricType}'.");
        }

        return ReadingValidationResult.Accept();
    }

    private static bool MetricAndUnitMatch(
        SensorMetricType metricType,
        MeasurementUnit unit)
    {
        return metricType switch
        {
            SensorMetricType.Temperature => unit == MeasurementUnit.Celsius,
            SensorMetricType.Humidity => unit == MeasurementUnit.Percent,
            SensorMetricType.WindSpeed => unit == MeasurementUnit.MetersPerSecond,
            _ => false
        };
    }

    private static bool ValueIsPlausible(SensorMetricType metricType, double value)
    {
        return metricType switch
        {
            SensorMetricType.Temperature => value >= -50.0 && value <= 80.0,
            SensorMetricType.Humidity => value >= 0.0 && value <= 100.0,
            SensorMetricType.WindSpeed => value >= 0.0 && value <= 100.0,
            _ => false
        };
    }
}