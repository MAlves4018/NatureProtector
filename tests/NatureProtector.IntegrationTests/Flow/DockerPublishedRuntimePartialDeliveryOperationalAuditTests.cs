using Microsoft.EntityFrameworkCore;
using NatureProtector.Core.Scenarios;
using NatureProtector.Infrastructure.Postgres.Pipeline;
using NatureProtector.IntegrationTests.TestInfrastructure;
using NatureProtector.Prevention.Host.Processing;
using RabbitMQ.Client;
using System.Text;

namespace NatureProtector.IntegrationTests.Flow;

public sealed partial class DockerPublishedRuntimeProcessTests
{
    private const string Phase3GProcessAuditEnvironmentVariable = "NP_RUN_OPERATIONAL_AUDIT_PHASE3G";

    [Fact]
    [Trait("Category", "DockerIntegration")]
    [Trait("Purpose", "OperationalAudit")]
    public async Task PublishedSimulator_PartialNack_ExitsNonZero_MarksRunFailed_WhilePrimaryProcessesOnce()
    {
        if (!IsPhase3GProcessAuditEnabled())
        {
            return;
        }

        await using var database = await TemporaryPostgresDatabase.CreateAsync();
        var exchangeName = $"np.it.phase3g.process.{Guid.NewGuid():N}";
        var baseOptions = DockerIntegrationSettings.CreateRabbitMqOptions(
            exchangeName,
            observabilityRawEnabled: true);
        await using var virtualHost = await TemporaryRabbitMqVirtualHost.CreateAsync(
            baseOptions,
            CancellationToken.None);
        var rabbitMqOptions = virtualHost.CreateOptions(exchangeName);
        var connectionFactory = virtualHost.CreateConnectionFactory();
        var repositoryRoot = ResolveRepositoryRoot();
        var runRoot = Path.Combine(
            repositoryRoot,
            "artifacts",
            "tests",
            "rabbitmq-health-phase3g",
            Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(runRoot);

        var seeded = await SeedProcessSmokeControlPlaneAsync(database);
        var postgresSettings = DockerIntegrationSettings.CreatePostgresSettings(database.DatabaseName);
        var environment = CreateRuntimeEnvironment(
                postgresSettings,
                rabbitMqOptions,
                seeded.AreaId)
            .ToDictionary(pair => pair.Key, pair => pair.Value, StringComparer.Ordinal);
        environment["RabbitMq__ObservabilityRawEnabled"] = "true";

        var preventionPublishDirectory = Path.Combine(runRoot, "prevention");
        var simulatorPublishDirectory = Path.Combine(runRoot, "simulator");

        await PublishProjectAsync(
            repositoryRoot,
            "src/NatureProtector.Prevention.Host/NatureProtector.Prevention.Host.csproj",
            preventionPublishDirectory,
            Path.Combine(runRoot, "publish-prevention"));
        await PublishProjectAsync(
            repositoryRoot,
            "src/NatureProtector.Simulator.Host/NatureProtector.Simulator.Host.csproj",
            simulatorPublishDirectory,
            Path.Combine(runRoot, "publish-simulator"));

        var policyName = $"np-audit-phase3g-process-{Guid.NewGuid():N}";
        StartedProcess? preventionProcess = null;
        StartedProcess? simulatorProcess = null;

        try
        {
            preventionProcess = StartPublishedProcess(
                "Prevention Host Phase 3G",
                preventionPublishDirectory,
                "NatureProtector.Prevention.Host.dll",
                WithAspNetCoreUrl(environment, GetFreeTcpPort()),
                Path.Combine(runRoot, "prevention"));
            await WaitForConsumerAsync(
                connectionFactory,
                rabbitMqOptions.IngestionReadingsQueueName);

            await virtualHost.SetQueuePolicyAsync(
                policyName,
                rabbitMqOptions.ObservabilityRawQueueName,
                new Dictionary<string, object>
                {
                    ["max-length"] = 1,
                    ["overflow"] = "reject-publish"
                },
                cancellationToken: CancellationToken.None);
            await Task.Delay(TimeSpan.FromMilliseconds(500));
            FillPhase3GRawQueue(connectionFactory, rabbitMqOptions.ObservabilityRawQueueName);

            simulatorProcess = StartPublishedProcess(
                "Simulator Host Phase 3G",
                simulatorPublishDirectory,
                "NatureProtector.Simulator.Host.dll",
                environment,
                Path.Combine(runRoot, "simulator"));

            var simulatorExitCode = await simulatorProcess.WaitForExitAndCaptureAsync(
                TimeSpan.FromSeconds(90));

            Assert.NotEqual(0, simulatorExitCode);

            await WaitForPhase3GFailedRunAndSingleProcessedEffectAsync(
                database,
                seeded.AreaId,
                seeded.GridCellId);

            await preventionProcess.StopAsync();
            AssertNoResidualIngestionMessage(
                connectionFactory,
                rabbitMqOptions.IngestionReadingsQueueName);

            var simulatorLogs =
                (await File.ReadAllTextAsync(simulatorProcess.StandardOutputPath)) +
                Environment.NewLine +
                (await File.ReadAllTextAsync(simulatorProcess.StandardErrorPath));

            Assert.Contains(
                nameof(NatureProtector.Simulator.Host.Publishing.RabbitMqPublishOutcomeUnknownException),
                simulatorLogs,
                StringComparison.Ordinal);
            Assert.Contains(
                "PossiblePartialDelivery=True",
                simulatorLogs,
                StringComparison.OrdinalIgnoreCase);

            using var connection = connectionFactory.CreateConnection("np-phase3g-process-raw-state");
            using var channel = connection.CreateModel();
            var rawState = channel.QueueDeclarePassive(rabbitMqOptions.ObservabilityRawQueueName);
            Assert.Equal(1u, rawState.MessageCount);

            Console.WriteLine(
                "PHASE3G_PUBLISHED_RUNTIME_PARTIAL_DELIVERY_PROVED " +
                $"simulator_exit={simulatorExitCode} run_status=Failed " +
                "primary_processed=1 inbox_attempts=1 raw_ready=1");
        }
        finally
        {
            if (simulatorProcess is not null)
            {
                await simulatorProcess.StopAsync();
            }

            if (preventionProcess is not null)
            {
                await preventionProcess.StopAsync();
            }

            await virtualHost.ClearPolicyAsync(policyName, CancellationToken.None);
        }

        AssertProcessStopped(simulatorProcess);
        AssertProcessStopped(preventionProcess);
    }

    private static void FillPhase3GRawQueue(ConnectionFactory factory, string rawQueueName)
    {
        using var connection = factory.CreateConnection("np-phase3g-process-fill-raw");
        using var channel = connection.CreateModel();
        var properties = channel.CreateBasicProperties();
        properties.Persistent = true;
        properties.MessageId = $"phase3g-process-filler-{Guid.NewGuid():N}";

        channel.BasicPublish(
            exchange: string.Empty,
            routingKey: rawQueueName,
            mandatory: true,
            basicProperties: properties,
            body: Encoding.UTF8.GetBytes("phase3g process raw capacity filler"));

        Assert.Equal(1u, channel.QueueDeclarePassive(rawQueueName).MessageCount);
    }

    private static async Task WaitForPhase3GFailedRunAndSingleProcessedEffectAsync(
        TemporaryPostgresDatabase database,
        Guid areaId,
        Guid gridCellId)
    {
        string? lastState = null;

        for (var attempt = 0; attempt < 120; attempt++)
        {
            await using var dbContext = database.CreateDbContext();
            var runs = await dbContext.SimulationRuns
                .Where(entity => entity.AreaId == areaId)
                .ToListAsync();
            var inboxEvents = await dbContext.InboxEvents.ToListAsync();
            var attempts = await dbContext.ProcessingAttempts.ToListAsync();
            var acceptedReadings = await dbContext.AcceptedReadingLogs.CountAsync();
            var assessments = await dbContext.RiskAssessmentLogs.CountAsync();
            var snapshots = await dbContext.AreaRiskSnapshotLogs.CountAsync(entity => entity.AreaId == areaId);
            var cellStates = await dbContext.CellOperationalStates.CountAsync(entity => entity.GridCellId == gridCellId);
            var areaStates = await dbContext.AreaOperationalStates.CountAsync(entity => entity.AreaId == areaId);

            lastState =
                $"runs=[{string.Join(",", runs.Select(run => run.Status))}] " +
                $"ended=[{string.Join(",", runs.Select(run => run.EndedAt.HasValue))}] " +
                $"inbox=[{string.Join(",", inboxEvents.Select(inbox => inbox.Status))}] " +
                $"attempts=[{string.Join(",", attempts.Select(processingAttempt => processingAttempt.Outcome))}] " +
                $"accepted={acceptedReadings} assessments={assessments} snapshots={snapshots} " +
                $"cellStates={cellStates} areaStates={areaStates}";

            if (runs.Count == 1 &&
                runs[0].Status == SimulationRunStatus.Failed &&
                runs[0].EndedAt.HasValue &&
                inboxEvents.Count == 1 &&
                inboxEvents[0].Status == InboxEventStatus.Processed &&
                inboxEvents[0].AttemptCount == 1 &&
                attempts.Count == 1 &&
                attempts[0].Outcome == ProcessingAttemptOutcome.Succeeded &&
                acceptedReadings == 1 &&
                assessments == 1 &&
                snapshots == 1 &&
                cellStates == 1 &&
                areaStates == 1)
            {
                return;
            }

            await Task.Delay(TimeSpan.FromMilliseconds(250));
        }

        throw new TimeoutException(
            "Phase 3G published runtime did not reach the expected failed-run/single-effect state. " +
            $"Last state: {lastState}");
    }

    private static bool IsPhase3GProcessAuditEnabled()
        => string.Equals(
            Environment.GetEnvironmentVariable(Phase3GProcessAuditEnvironmentVariable),
            "true",
            StringComparison.OrdinalIgnoreCase);
}
