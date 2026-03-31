using System.Text;
using NatureProtector.Shared.Contracts.Readings;
using NatureProtector.Shared.Messaging;

/*
 * This publisher writes generated reading envelopes to the application logs
 * and to the console.
 *
 * Rationale:
 * - For Day 4, a console publisher is the simplest way to validate that the
 *   simulator is generating plausible structured events.
 * - It allows the simulation pipeline to be exercised end-to-end without yet
 *   requiring a broker-specific publisher implementation.
 *
 * Design considerations:
 * - The publisher logs a compact summary and also emits the serialized JSON
 *   payload for inspection.
 * - The implementation is intentionally simple and side-effect free apart from
 *   console and logging output.
 */

namespace NatureProtector.Simulator.Host.Publishing;

public sealed class ConsoleReadingPublisher(
    ILogger<ConsoleReadingPublisher> logger) : IReadingPublisher
{
    /// <summary>
    /// Publishes one reading envelope by writing a summary and the serialized
    /// JSON payload to the console.
    /// </summary>
    /// <param name="envelope">
    /// Envelope to publish.
    /// </param>
    /// <param name="cancellationToken">
    /// Cancellation token for cooperative shutdown.
    /// </param>
    public Task PublishAsync(
        EventEnvelope<SensorReadingProducedPayload> envelope,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(envelope);

        cancellationToken.ThrowIfCancellationRequested();

        var json = Encoding.UTF8.GetString(
            JsonEventSerializer.SerializeToUtf8Bytes(envelope));

        logger.LogInformation(
            "Publishing reading to console | EventId={EventId} | SensorId={SensorId} | SensorName={SensorName} | MetricType={MetricType} | Value={Value} | State={State}",
            envelope.EventId,
            envelope.Payload.SensorId,
            envelope.Payload.SensorName,
            envelope.Payload.MetricType,
            envelope.Payload.Value,
            envelope.Payload.OperationalState);

        Console.WriteLine(json);

        return Task.CompletedTask;
    }
}