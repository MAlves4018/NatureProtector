using Microsoft.EntityFrameworkCore;
using NatureProtector.Infrastructure.Postgres.Pipeline;
using NatureProtector.IntegrationTests.TestInfrastructure;
using NatureProtector.Prevention.Host.Processing;
using NatureProtector.Simulator.Host.Publishing;
using RabbitMQ.Client;
using System.Text;

namespace NatureProtector.IntegrationTests.Flow;

public sealed partial class DockerRabbitMqConsumerPipelineTests
{
    private const string Phase3GAuditEnvironmentVariable = "NP_RUN_OPERATIONAL_AUDIT_PHASE3G";

    [Fact]
    [Trait("Category", "DockerIntegration")]
    [Trait("Purpose", "OperationalAudit")]
    public async Task PartialNack_PrimaryProcessesOnce_AndSameEventIdRetryIsIdempotent()
    {
        if (!IsPhase3GAuditEnabled())
        {
            return;
        }

        await using var harness = await ConsumerPipelineHarness.CreateAsync(
            observabilityRawEnabled: true);
        var policyName = $"np-audit-phase3g-reject-{Guid.NewGuid():N}";

        await harness.VirtualHost.SetQueuePolicyAsync(
            policyName,
            harness.RabbitMqOptions.ObservabilityRawQueueName,
            new Dictionary<string, object>
            {
                ["max-length"] = 1,
                ["overflow"] = "reject-publish"
            },
            cancellationToken: CancellationToken.None);

        try
        {
            await Task.Delay(TimeSpan.FromMilliseconds(500));
            FillOnlyRawQueue(harness.ConnectionFactory, harness.RabbitMqOptions.ObservabilityRawQueueName);

            var envelope = CreateEnvelope(
                harness.AreaId,
                harness.SensorId,
                harness.Timestamp,
                harness.SimulationRunId);

            var firstFailure = await Record.ExceptionAsync(() =>
                harness.Publisher.PublishAsync(envelope, CancellationToken.None));

            var firstAmbiguous = Assert.IsType<RabbitMqPublishOutcomeUnknownException>(firstFailure);
            Assert.True(firstAmbiguous.PossiblePartialDelivery);
            Assert.Equal(envelope.EventId.ToString(), firstAmbiguous.MessageId);
            Assert.Equal(
                harness.RabbitMqOptions.IngestionReadingsQueueName,
                firstAmbiguous.PrimaryQueueName);

            await WaitForProcessedEventAsync(
                harness.Database,
                envelope.EventId,
                harness.AreaId,
                harness.GridCellId);

            var retryFailure = await Record.ExceptionAsync(() =>
                harness.Publisher.PublishAsync(envelope, CancellationToken.None));

            var retryAmbiguous = Assert.IsType<RabbitMqPublishOutcomeUnknownException>(retryFailure);
            Assert.Equal(envelope.EventId.ToString(), retryAmbiguous.MessageId);

            await Task.Delay(TimeSpan.FromMilliseconds(750));
            await harness.StopWorkerAsync();

            AssertNoResidualIngestionMessage(
                harness.ConnectionFactory,
                harness.RabbitMqOptions.IngestionReadingsQueueName);
            await AssertSingleProcessedEffectAsync(
                harness.Database,
                envelope.EventId,
                harness.AreaId,
                harness.GridCellId);
            await AssertSingleInboxAttemptAsync(harness.Database, envelope.EventId);

            using var connection = harness.ConnectionFactory.CreateConnection("np-phase3g-raw-state");
            using var channel = connection.CreateModel();
            var rawState = channel.QueueDeclarePassive(
                harness.RabbitMqOptions.ObservabilityRawQueueName);

            Assert.Equal(1u, rawState.MessageCount);

            Console.WriteLine(
                "PHASE3G_PARTIAL_DELIVERY_IDEMPOTENCY_PROVED " +
                $"event_id={envelope.EventId} " +
                "publisher_failures=2 primary_processed=1 inbox_attempts=1 " +
                "accepted_readings=1 assessments=1 snapshots=1");
        }
        finally
        {
            await harness.VirtualHost.ClearPolicyAsync(
                policyName,
                CancellationToken.None);
        }
    }

    private static void FillOnlyRawQueue(ConnectionFactory factory, string rawQueueName)
    {
        using var connection = factory.CreateConnection("np-phase3g-fill-raw-only");
        using var channel = connection.CreateModel();
        var properties = channel.CreateBasicProperties();
        properties.Persistent = true;
        properties.MessageId = $"phase3g-raw-filler-{Guid.NewGuid():N}";

        channel.BasicPublish(
            exchange: string.Empty,
            routingKey: rawQueueName,
            mandatory: true,
            basicProperties: properties,
            body: Encoding.UTF8.GetBytes("phase3g raw capacity filler"));

        var rawState = channel.QueueDeclarePassive(rawQueueName);
        Assert.Equal(1u, rawState.MessageCount);
    }

    private static async Task AssertSingleInboxAttemptAsync(
        TemporaryPostgresDatabase database,
        Guid eventId)
    {
        await using var dbContext = database.CreateDbContext();
        var inbox = await dbContext.InboxEvents.SingleAsync(entity => entity.EventId == eventId);
        var attempts = await dbContext.ProcessingAttempts
            .Where(entity => entity.InboxEventId == inbox.Id)
            .OrderBy(entity => entity.AttemptNumber)
            .ToListAsync();

        Assert.Equal(InboxEventStatus.Processed, inbox.Status);
        Assert.Equal(1, inbox.AttemptCount);
        Assert.Single(attempts);
        Assert.Equal(ProcessingAttemptOutcome.Succeeded, attempts[0].Outcome);
    }

    private static bool IsPhase3GAuditEnabled()
        => string.Equals(
            Environment.GetEnvironmentVariable(Phase3GAuditEnvironmentVariable),
            "true",
            StringComparison.OrdinalIgnoreCase);
}
