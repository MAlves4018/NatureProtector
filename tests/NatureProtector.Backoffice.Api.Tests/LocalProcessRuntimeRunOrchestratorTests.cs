using System.Collections.Concurrent;
using Microsoft.AspNetCore.Hosting;
using Microsoft.Extensions.FileProviders;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using NatureProtector.Backoffice.Api.RuntimeOrchestration;

namespace NatureProtector.Backoffice.Api.Tests;

public sealed class LocalProcessRuntimeRunOrchestratorTests
{
    [Fact]
    public async Task StartAsync_InvalidLaunchModeReturnsFailedReceiptAndWritesEvidenceError()
    {
        var evidence = new RecordingRuntimeEvidenceSink();
        using var orchestrator = CreateOrchestrator(
            evidence,
            new RuntimeOrchestrationOptions { LaunchMode = "unsupported" });
        var request = Request("invalid-mode");

        var error = await Assert.ThrowsAsync<InvalidOperationException>(() =>
            orchestrator.StartAsync(request, CancellationToken.None));
        var snapshot = await orchestrator.GetAsync(request.ExecutionId, CancellationToken.None);

        Assert.Equal("local-process", orchestrator.Provider);
        Assert.True(orchestrator.IsAvailable);
        Assert.Contains("unsupported", error.Message, StringComparison.Ordinal);
        Assert.Null(snapshot);
        Assert.Empty(evidence.Text);
    }

    [Fact]
    public async Task StartAsync_NonZeroProcessExitCapturesOutputSnapshotAndIdempotency()
    {
        var evidence = new RecordingRuntimeEvidenceSink();
        using var orchestrator = CreateOrchestrator(
            evidence,
            new RuntimeOrchestrationOptions
            {
                LaunchMode = RuntimeProcessLaunchModes.PublishedAssembly,
                ExecutablePath = "dotnet",
                WorkingDirectory = Directory.GetCurrentDirectory(),
                SimulatorAssemblyPath = Path.Combine(Path.GetTempPath(), $"missing-{Guid.NewGuid():N}.dll"),
                MaximumTimeoutSeconds = 10
            });
        var request = Request("missing-assembly");

        var first = await orchestrator.StartAsync(request, CancellationToken.None);
        var second = await orchestrator.StartAsync(request with
        {
            ExecutionId = new RuntimeExecutionId(Guid.NewGuid())
        }, CancellationToken.None);
        var snapshot = await orchestrator.GetAsync(request.ExecutionId, CancellationToken.None);
        var terminalStop = await orchestrator.StopAsync(request.ExecutionId, RuntimeStopReason.UserRequest, CancellationToken.None);
        var missingStop = await orchestrator.StopAsync(
            new RuntimeExecutionId(Guid.NewGuid()),
            RuntimeStopReason.UserRequest,
            CancellationToken.None);

        Assert.Equal(RuntimeExecutionState.Failed, first.State);
        Assert.False(first.ReusedExistingExecution);
        Assert.True(second.ReusedExistingExecution);
        Assert.Equal(first.ExecutionId, second.ExecutionId);
        Assert.Equal(RuntimeExecutionState.Failed, snapshot!.State);
        Assert.Equal("process_exit_nonzero", snapshot.FailureCode);
        Assert.NotEqual(0, snapshot.ExitCode);
        Assert.NotNull(snapshot.StartedAtUtc);
        Assert.NotNull(snapshot.FinishedAtUtc);
        Assert.Contains("simulator-host.stdout.log", evidence.Text.Keys);
        Assert.Contains("simulator-host.stderr.log", evidence.Text.Keys);
        Assert.Contains("process-exit.json", evidence.Json.Keys);
        Assert.False(terminalStop.StopAccepted);
        Assert.Equal(RuntimeExecutionState.Failed, terminalStop.State);
        Assert.False(missingStop.StopAccepted);
        Assert.Equal(RuntimeExecutionState.Unknown, missingStop.State);
    }

    [Fact]
    public async Task StartAsync_ProjectModeWithoutRepositoryFailsBeforeProcessLaunch()
    {
        var evidence = new RecordingRuntimeEvidenceSink();
        using var tempRoot = new TemporaryDirectory();
        using var orchestrator = CreateOrchestrator(
            evidence,
            new RuntimeOrchestrationOptions
            {
                LaunchMode = RuntimeProcessLaunchModes.Project,
                ExecutablePath = "dotnet",
                WorkingDirectory = tempRoot.Path
            });
        var request = Request("project-mode") with { Evidence = null };

        var receipt = await orchestrator.StartAsync(request, CancellationToken.None);
        var snapshot = await orchestrator.GetAsync(request.ExecutionId, CancellationToken.None);

        Assert.Equal(RuntimeExecutionState.Failed, receipt.State);
        Assert.Equal("process_exit_nonzero", snapshot!.FailureCode);
        Assert.Empty(evidence.Text);
    }

    private static LocalProcessRuntimeRunOrchestrator CreateOrchestrator(
        RecordingRuntimeEvidenceSink evidenceSink,
        RuntimeOrchestrationOptions options)
    {
        return new LocalProcessRuntimeRunOrchestrator(
            Options.Create(options),
            evidenceSink,
            new TestHostEnvironment(Directory.GetCurrentDirectory()),
            NullLogger<LocalProcessRuntimeRunOrchestrator>.Instance);
    }

    private static RuntimeLaunchRequest Request(string idempotencyKey) => new(
        new RuntimeExecutionId(Guid.NewGuid()),
        Guid.NewGuid(),
        idempotencyKey,
        "local",
        RuntimeLaunchProfile.Simulation,
        new RuntimeSimulationParameters("PT-11", "scenario_a", 1, 1, 1, 123, null, ["none"], $"corr-{idempotencyKey}"),
        null,
        CollectEvidence: true,
        WaitForCompletion: true,
        TimeSpan.FromSeconds(5),
        new RuntimeEvidenceReference($"evidence-{idempotencyKey}", $"location-{idempotencyKey}"));

    private sealed class RecordingRuntimeEvidenceSink : IRuntimeEvidenceSink
    {
        public bool IsAvailable => true;
        public string AvailabilityMessage => "test evidence sink";
        public ConcurrentDictionary<string, string> Text { get; } = new(StringComparer.Ordinal);
        public ConcurrentDictionary<string, object> Json { get; } = new(StringComparer.Ordinal);

        public Task<RuntimeEvidenceReference> CreateAsync(
            string category,
            DateTimeOffset requestedAtUtc,
            string label,
            CancellationToken cancellationToken)
            => Task.FromResult(new RuntimeEvidenceReference($"evidence-{label}", category));

        public Task WriteJsonAsync(
            RuntimeEvidenceReference evidence,
            string fileName,
            object value,
            CancellationToken cancellationToken)
        {
            Json[fileName] = value;
            return Task.CompletedTask;
        }

        public Task WriteTextAsync(
            RuntimeEvidenceReference evidence,
            string fileName,
            string value,
            CancellationToken cancellationToken)
        {
            Text[fileName] = value;
            return Task.CompletedTask;
        }
    }

    private sealed class TestHostEnvironment(string contentRootPath) : IHostEnvironment, IWebHostEnvironment
    {
        public string EnvironmentName { get; set; } = Environments.Development;
        public string ApplicationName { get; set; } = "NatureProtector.Backoffice.Api.Tests";
        public string ContentRootPath { get; set; } = contentRootPath;
        public IFileProvider ContentRootFileProvider { get; set; } = new NullFileProvider();
        public string WebRootPath { get; set; } = contentRootPath;
        public IFileProvider WebRootFileProvider { get; set; } = new NullFileProvider();
    }

    private sealed class TemporaryDirectory : IDisposable
    {
        public TemporaryDirectory()
        {
            Path = System.IO.Path.Combine(
                System.IO.Path.GetTempPath(),
                $"np-local-orchestrator-{Guid.NewGuid():N}");
            Directory.CreateDirectory(Path);
        }

        public string Path { get; }

        public void Dispose()
        {
            if (Directory.Exists(Path))
            {
                Directory.Delete(Path, recursive: true);
            }
        }
    }
}
