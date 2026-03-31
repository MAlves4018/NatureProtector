using NatureProtector.Shared.Contracts.Readings;
using NatureProtector.Shared.Messaging;

/*
 * This interface abstracts the publication of generated readings.
 *
 * Rationale:
 * - The simulator should not know whether a reading is written to the console,
 *   published to RabbitMQ or sent elsewhere.
 * - Decoupling publication behind an interface makes Day 4 simple and allows
 *   later replacement without changing orchestration code.
 *
 * Design considerations:
 * - The contract is asynchronous because future publishers may involve I/O.
 * - The publisher receives a fully built envelope, so all message creation
 *   remains the responsibility of the generation layer.
 */

namespace NatureProtector.Simulator.Host.Publishing;

public interface IReadingPublisher
{
    /// <summary>
    /// Publishes one generated reading envelope.
    /// </summary>
    /// <param name="envelope">
    /// Envelope to publish.
    /// </param>
    /// <param name="cancellationToken">
    /// Cancellation token for cooperative shutdown.
    /// </param>
    Task PublishAsync(
        EventEnvelope<SensorReadingProducedPayload> envelope,
        CancellationToken cancellationToken = default);
}