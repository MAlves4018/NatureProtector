using NatureProtector.Shared.Messaging;

/*
 * This class contains the Prevention.Host runtime options related to
 * queue consumption and accepted reading persistence.
 *
 * Rationale:
 * - The host needs a small, explicit configuration surface for Day 5.
 * - Keeping these settings together avoids scattering queue and persistence
 *   configuration across the worker and infrastructure services.
 *
 * Design considerations:
 * - Defaults are aligned with the current RabbitMQ topology and local
 *   development workflow.
 * - AcceptedReadingsPath is intentionally file-based for this phase in order
 *   to keep persistence simple and inspectable.
 */

namespace NatureProtector.Prevention.Host.Configuration;

public sealed class PreventionOptions
{
    public const string SectionName = "Prevention";

    /// <summary>
    /// Queue from which the host consumes raw sensor readings.
    /// </summary>
    public string QueueName { get; set; } =
        NatureProtectorRabbitMqTopology.IngestionReadingsQueue;

    /// <summary>
    /// RabbitMQ prefetch count used by the consumer.
    /// </summary>
    public ushort PrefetchCount { get; set; } = 10;

    /// <summary>
    /// Indicates whether unexpected processing failures should requeue the message.
    /// </summary>
    public bool RequeueOnUnexpectedFailure { get; set; } = true;

    /// <summary>
    /// Relative or absolute path where accepted readings are persisted as NDJSON.
    /// </summary>
    public string AcceptedReadingsPath { get; set; } = "data/accepted-readings.ndjson";
}