using System.Reflection;
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

public sealed class ControlledValidationRunnerTests
{
    [Fact]
    public async Task ExecuteAsync_StopsApplication_AfterSuccessfulPublication()
    {
        var publisher = new RecordingControlledValidationMessagePublisher();
        var lifetime = new RecordingApplicationLifetime();
        var runner = new ControlledValidationRunner(
            NullLogger<ControlledValidationRunner>.Instance,
            CreateOrchestrator("Evidence", publisher),
            lifetime);

        await InvokeExecuteAsync(runner);

        Assert.True(lifetime.StopApplicationCalled);
        Assert.Equal(6, publisher.Messages.Count);
    }

    [Fact]
    public async Task ExecuteAsync_StopsApplication_WhenPublicationFails()
    {
        var publisher = new RecordingControlledValidationMessagePublisher();
        var lifetime = new RecordingApplicationLifetime();
        var runner = new ControlledValidationRunner(
            NullLogger<ControlledValidationRunner>.Instance,
            CreateOrchestrator("Production", publisher),
            lifetime);

        await Assert.ThrowsAsync<InvalidOperationException>(() => InvokeExecuteAsync(runner));

        Assert.True(lifetime.StopApplicationCalled);
        Assert.Empty(publisher.Messages);
    }

    private static async Task InvokeExecuteAsync(ControlledValidationRunner runner)
    {
        var method = typeof(ControlledValidationRunner).GetMethod(
            "ExecuteAsync",
            BindingFlags.Instance | BindingFlags.NonPublic);
        Assert.NotNull(method);
        var task = (Task?)method.Invoke(runner, [CancellationToken.None]);
        Assert.NotNull(task);
        await task;
    }

    private static ControlledValidationOrchestrator CreateOrchestrator(
        string environmentName,
        RecordingControlledValidationMessagePublisher publisher)
    {
        var options = Options.Create(new ControlledValidationOptions
        {
            Enabled = true,
            RunLabel = "p0-smoke",
            ControlledValidationRunId = Guid.Parse("aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaaaaa"),
            EventTime = new DateTimeOffset(2026, 6, 4, 12, 0, 0, TimeSpan.Zero),
            EvidenceOutputRoot = publisher.EvidenceOutputRoot
        });
        var factory = new ControlledValidationManifestFactory(
            options,
            Options.Create(ControlledValidationManifestFactoryTests.CreateSimulatorOptions()));

        return new ControlledValidationOrchestrator(
            NullLogger<ControlledValidationOrchestrator>.Instance,
            new TestHostEnvironment(environmentName),
            factory,
            new ControlledValidationEvidenceWriter(
                NullLogger<ControlledValidationEvidenceWriter>.Instance,
                new TestHostEnvironment(environmentName),
                options),
            new TestSimulationContextSource(),
            new RecordingSimulationRunStore(),
            publisher);
    }

    private sealed class RecordingControlledValidationMessagePublisher : IControlledValidationMessagePublisher
    {
        public string EvidenceOutputRoot { get; } = Path.Combine(
            Path.GetTempPath(),
            "natureprotector-controlled-validation-runner-tests",
            Guid.NewGuid().ToString("N"));

        public List<ControlledValidationMessage> Messages { get; } = [];

        public Task PublishAsync(
            ControlledValidationMessage message,
            CancellationToken cancellationToken = default)
        {
            Messages.Add(message);
            return Task.CompletedTask;
        }
    }

    private sealed class RecordingSimulationRunStore : ISimulationRunStore
    {
        public List<SimulationRunStatus> Statuses { get; } = [];

        public Task UpsertAsync(
            SimulationContext context,
            SimulationRun run,
            CancellationToken cancellationToken)
        {
            Statuses.Add(run.Status);
            return Task.CompletedTask;
        }
    }

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

    private sealed class RecordingApplicationLifetime : IHostApplicationLifetime
    {
        public CancellationToken ApplicationStarted => CancellationToken.None;

        public CancellationToken ApplicationStopping => CancellationToken.None;

        public CancellationToken ApplicationStopped => CancellationToken.None;

        public bool StopApplicationCalled { get; private set; }

        public void StopApplication()
        {
            StopApplicationCalled = true;
        }
    }
}
