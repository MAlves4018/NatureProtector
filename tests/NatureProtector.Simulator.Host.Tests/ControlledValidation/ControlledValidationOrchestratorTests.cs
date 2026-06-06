using Microsoft.Extensions.FileProviders;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using NatureProtector.Core.Primitives;
using NatureProtector.Core.Scenarios;
using NatureProtector.Core.Sensors;
using NatureProtector.Simulator.Host.ControlledValidation;
using NatureProtector.Simulator.Host.Services;

namespace NatureProtector.Simulator.Host.Tests.ControlledValidation;

public sealed class ControlledValidationOrchestratorTests
{
    [Fact]
    public async Task PublishP0Async_PublishesBuiltMessages_WhenEnvironmentIsAllowed()
    {
        var runStore = new RecordingSimulationRunStore();
        var publisher = new RecordingControlledValidationMessagePublisher(
            () => runStore.Records.Any(record => record.Status == SimulationRunStatus.Running));
        var orchestrator = CreateOrchestrator("Development", publisher, runStore: runStore);

        var manifest = await orchestrator.PublishP0Async(CancellationToken.None);

        Assert.Equal("p0-smoke", manifest.RunLabel);
        Assert.Equal(6, publisher.Messages.Count);
        Assert.True(publisher.FirstPublishObservedRegisteredRun);
        Assert.Equal(
            [SimulationRunStatus.Ready, SimulationRunStatus.Running, SimulationRunStatus.Completed],
            runStore.Records.Select(record => record.Status).ToArray());
        Assert.All(runStore.Records, record =>
        {
            Assert.Equal(manifest.SimulationRunId, record.RunId);
            Assert.Equal("controlled-validation:p0-smoke", record.Context.RunOverrides?.Resolved.OrchestratorCorrelationId);
        });
        Assert.Contains(
            Directory.GetFiles(publisher.EvidenceOutputRoot, "expected-outcomes.json", SearchOption.AllDirectories),
            path => path.EndsWith("expected-outcomes.json", StringComparison.Ordinal));
        Assert.Contains(
            Directory.GetFiles(publisher.EvidenceOutputRoot, "expected-outcomes.csv", SearchOption.AllDirectories),
            path => path.EndsWith("expected-outcomes.csv", StringComparison.Ordinal));
        Assert.Contains(publisher.Messages, message =>
            message.FaultCase.FaultCaseId == ControlledValidationFaultCaseIds.N1InvalidJson);
        Assert.Contains(publisher.Messages, message =>
            message.FaultCase.FaultCaseId == ControlledValidationFaultCaseIds.N4DuplicatePayloadMismatch &&
            message.IsSetupMessage);
    }

    [Fact]
    public async Task PublishP0Async_ThrowsAndDoesNotPublish_WhenEnvironmentIsNotAllowed()
    {
        var runStore = new RecordingSimulationRunStore();
        var publisher = new RecordingControlledValidationMessagePublisher();
        var orchestrator = CreateOrchestrator("Production", publisher, runStore: runStore);

        await Assert.ThrowsAsync<InvalidOperationException>(() =>
            orchestrator.PublishP0Async(CancellationToken.None));

        Assert.Empty(publisher.Messages);
        Assert.Empty(runStore.Records);
    }

    [Fact]
    public async Task PublishP0Async_ThrowsAndDoesNotPublish_WhenSimulationRunCannotBeGuaranteed()
    {
        var runStore = new RecordingSimulationRunStore
        {
            ThrowOnUpsert = true
        };
        var publisher = new RecordingControlledValidationMessagePublisher();
        var orchestrator = CreateOrchestrator("Evidence", publisher, runStore: runStore);

        await Assert.ThrowsAsync<InvalidOperationException>(() =>
            orchestrator.PublishP0Async(CancellationToken.None));

        Assert.Empty(publisher.Messages);
        Assert.False(Directory.Exists(publisher.EvidenceOutputRoot));
    }

    private static ControlledValidationOrchestrator CreateOrchestrator(
        string environmentName,
        RecordingControlledValidationMessagePublisher publisher,
        TestSimulationContextSource? contextSource = null,
        RecordingSimulationRunStore? runStore = null)
    {
        var factory = new ControlledValidationManifestFactory(
            Options.Create(new ControlledValidationOptions
            {
                Enabled = true,
                RunLabel = "p0-smoke",
                ControlledValidationRunId = Guid.Parse("aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaaaaa"),
                EventTime = new DateTimeOffset(2026, 6, 4, 12, 0, 0, TimeSpan.Zero),
                EvidenceOutputRoot = publisher.EvidenceOutputRoot
            }),
            Options.Create(ControlledValidationManifestFactoryTests.CreateSimulatorOptions()));
        var options = Options.Create(new ControlledValidationOptions
        {
            Enabled = true,
            RunLabel = "p0-smoke",
            ControlledValidationRunId = Guid.Parse("aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaaaaa"),
            EventTime = new DateTimeOffset(2026, 6, 4, 12, 0, 0, TimeSpan.Zero),
            EvidenceOutputRoot = publisher.EvidenceOutputRoot
        });
        var environment = new TestHostEnvironment(environmentName);

        return new ControlledValidationOrchestrator(
            NullLogger<ControlledValidationOrchestrator>.Instance,
            environment,
            factory,
            new ControlledValidationEvidenceWriter(
                NullLogger<ControlledValidationEvidenceWriter>.Instance,
                environment,
                options),
            contextSource ?? new TestSimulationContextSource(),
            runStore ?? new RecordingSimulationRunStore(),
            publisher);
    }

    private sealed class RecordingControlledValidationMessagePublisher(
        Func<bool>? isRunRegistered = null) : IControlledValidationMessagePublisher
    {
        public string EvidenceOutputRoot { get; } = Path.Combine(
            Path.GetTempPath(),
            "natureprotector-controlled-validation-tests",
            Guid.NewGuid().ToString("N"));

        public List<ControlledValidationMessage> Messages { get; } = [];

        public bool FirstPublishObservedRegisteredRun { get; private set; }

        public Task PublishAsync(
            ControlledValidationMessage message,
            CancellationToken cancellationToken = default)
        {
            FirstPublishObservedRegisteredRun = FirstPublishObservedRegisteredRun ||
                (isRunRegistered?.Invoke() ?? false);
            Messages.Add(message);
            return Task.CompletedTask;
        }
    }

    private sealed class RecordingSimulationRunStore : ISimulationRunStore
    {
        public List<RecordedSimulationRun> Records { get; } = [];

        public bool ThrowOnUpsert { get; init; }

        public Task UpsertAsync(
            SimulationContext context,
            SimulationRun run,
            CancellationToken cancellationToken)
        {
            if (ThrowOnUpsert)
            {
                throw new InvalidOperationException("controlled validation simulation_run could not be guaranteed.");
            }

            Records.Add(new RecordedSimulationRun(run.Id, run.Status, context));
            return Task.CompletedTask;
        }
    }

    private sealed record RecordedSimulationRun(
        Guid RunId,
        SimulationRunStatus Status,
        SimulationContext Context);

    private sealed class TestSimulationContextSource : ISimulationContextSource
    {
        public Task<SimulationContext> CreateAsync(CancellationToken cancellationToken)
        {
            var scenario = new Scenario(
                id: Guid.Parse("eeeeeeee-eeee-eeee-eeee-eeeeeeeeeeee"),
                name: "P0 controlled validation scenario",
                category: ScenarioCategory.Exercise,
                parameters: new ScenarioParameters(
                    baseTemperature: 28.0,
                    baseHumidity: 35.0,
                    baseWindSpeed: 6.0,
                    failureRate: 0.0,
                    noiseLevel: 0.0,
                    timeAcceleration: 1.0));
            var sensor = new Sensor(
                id: Guid.Parse("dddddddd-dddd-dddd-dddd-dddddddddddd"),
                name: "sensor-p0-001",
                type: SensorType.Temperature,
                location: new Location(39.8, -7.9),
                profile: new SensorProfile(
                    id: Guid.Parse("ffffffff-ffff-ffff-ffff-ffffffffffff"),
                    samplingInterval: TimeSpan.FromSeconds(5),
                    communicationMode: "RabbitMq",
                    noiseLevel: 0.0,
                    latencyProfile: "Low latency",
                    failureProfile: "Controlled validation"));

            return Task.FromResult(new SimulationContext(
                areaId: Guid.Parse("11111111-1111-1111-1111-111111111111"),
                scenario: scenario,
                sensors: [sensor],
                startTimestamp: new DateTimeOffset(2026, 6, 4, 12, 0, 0, TimeSpan.Zero),
                interval: TimeSpan.FromSeconds(5),
                numberOfCycles: 1,
                configurationVersionId: Guid.Parse("abababab-abab-abab-abab-abababababab"),
                scenarioCode: "scenario_b",
                preferredSeed: 12345));
        }
    }

    private sealed class TestHostEnvironment(string environmentName) : IHostEnvironment
    {
        public string EnvironmentName { get; set; } = environmentName;

        public string ApplicationName { get; set; } = "NatureProtector.Simulator.Host.Tests";

        public string ContentRootPath { get; set; } = AppContext.BaseDirectory;

        public IFileProvider ContentRootFileProvider { get; set; } = new NullFileProvider();
    }
}
