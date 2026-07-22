using System.Globalization;
using System.Net;
using System.Text;
using System.Text.Json;
using Microsoft.AspNetCore.Hosting;
using Microsoft.Extensions.FileProviders;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using NatureProtector.Backoffice.Api.RuntimeOrchestration;

namespace NatureProtector.Backoffice.Api.Tests;

public sealed class RuntimeOrchestrationEvidenceAndCloudRunTests
{
    [Fact]
    public async Task FileSystemRuntimeEvidenceSink_SanitizesFoldersWritesFilesAndRejectsTraversal()
    {
        var root = Path.Combine(Path.GetTempPath(), "np-runtime-evidence-tests", Guid.NewGuid().ToString("N"));
        try
        {
            var sink = new FileSystemRuntimeEvidenceSink(
                Options.Create(new RuntimeOrchestrationOptions { EvidenceRoot = root }),
                new TestHostEnvironment());

            var reference = await sink.CreateAsync(
                "runtime/unsafe category",
                DateTimeOffset.Parse("2026-07-21T12:34:56Z", CultureInfo.InvariantCulture),
                "label with spaces",
                CancellationToken.None);

            await sink.WriteTextAsync(reference, "notes.txt", "runtime evidence", CancellationToken.None);
            await sink.WriteJsonAsync(reference, "sample.json", new { accepted = 30 }, CancellationToken.None);
            var error = await Assert.ThrowsAsync<InvalidOperationException>(() =>
                sink.WriteTextAsync(reference, "../escape.txt", "escape", CancellationToken.None));

            Assert.True(sink.IsAvailable);
            Assert.Contains(root, sink.AvailabilityMessage, StringComparison.Ordinal);
            Assert.StartsWith("20260721-123456-label-with-spaces-", reference.EvidenceId, StringComparison.Ordinal);
            Assert.Equal(
                Path.Combine(root, "runtime", "unsafe-category", reference.EvidenceId),
                reference.Location);
            Assert.Equal("runtime evidence", await File.ReadAllTextAsync(Path.Combine(reference.Location, "notes.txt")));
            Assert.Contains("\"accepted\": 30", await File.ReadAllTextAsync(Path.Combine(reference.Location, "sample.json")));
            Assert.Equal("Runtime evidence fileName must not contain directory traversal.", error.Message);
            Assert.False(File.Exists(Path.Combine(root, "escape.txt")));
        }
        finally
        {
            if (Directory.Exists(root))
            {
                Directory.Delete(root, recursive: true);
            }
        }
    }

    [Fact]
    public async Task CloudRunJobRuntimeRunOrchestrator_ReusesExistingReservationWithoutStartingProvider()
    {
        var request = Request("same-key");
        var record = Record(request) with
        {
            ProviderOperationName = "projects/p/locations/europe-southwest1/operations/op-1",
            State = RuntimeExecutionState.Running
        };
        var store = new InMemoryCloudRunExecutionStore(new CloudRunExecutionReservation(
            record,
            Guid.NewGuid(),
            OwnsLaunch: false,
            ReusedExistingExecution: true));
        var gateway = new RecordingCloudRunGateway();
        var orchestrator = CreateCloudRunOrchestrator(store, gateway);

        var receipt = await orchestrator.StartAsync(request, CancellationToken.None);

        Assert.Equal("cloud-run-job", orchestrator.Provider);
        Assert.True(orchestrator.IsAvailable);
        Assert.Equal(RuntimeExecutionState.Running, receipt.State);
        Assert.True(receipt.ReusedExistingExecution);
        Assert.Equal(record.ProviderOperationName, receipt.ProviderReference);
        Assert.Equal(0, gateway.StartCount);
    }

    [Fact]
    public async Task CloudRunJobRuntimeRunOrchestrator_StartAttachesOperationAndRefreshesSuccessfulCompletion()
    {
        var request = Request("new-key");
        var store = new InMemoryCloudRunExecutionStore();
        var gateway = new RecordingCloudRunGateway
        {
            OperationName = "projects/p/locations/europe-southwest1/operations/op-2",
            Snapshot = new CloudRunOperationSnapshot(
                "projects/p/locations/europe-southwest1/operations/op-2",
                "projects/p/locations/europe-southwest1/jobs/natureprotector-simulator/executions/ex-1",
                Done: true,
                Failed: false,
                FailureCode: null,
                FailureMessage: null,
                DateTimeOffset.Parse("2026-07-21T12:00:00Z", CultureInfo.InvariantCulture),
                DateTimeOffset.Parse("2026-07-21T12:01:00Z", CultureInfo.InvariantCulture))
        };
        var orchestrator = CreateCloudRunOrchestrator(store, gateway);

        var receipt = await orchestrator.StartAsync(request, CancellationToken.None);
        var snapshot = await orchestrator.GetAsync(receipt.ExecutionId, CancellationToken.None);

        Assert.Equal(RuntimeExecutionState.Running, receipt.State);
        Assert.False(receipt.ReusedExistingExecution);
        Assert.Equal(gateway.OperationName, receipt.ProviderReference);
        Assert.Equal(RuntimeExecutionState.Succeeded, snapshot!.State);
        Assert.Null(snapshot.FailureCode);
        Assert.Equal(1, gateway.StartCount);
        Assert.Equal(1, gateway.GetCount);
        Assert.Equal(request.Simulation.OrchestratorCorrelationId, snapshot.LogCorrelation);
    }

    [Fact]
    public async Task CloudRunJobRuntimeRunOrchestrator_AttachLeaseLossCancelsProviderOperation()
    {
        var request = Request("lost-lease");
        var store = new InMemoryCloudRunExecutionStore { AttachShouldSucceed = false };
        var gateway = new RecordingCloudRunGateway
        {
            OperationName = "projects/p/locations/europe-southwest1/operations/op-3"
        };
        var orchestrator = CreateCloudRunOrchestrator(store, gateway);

        var receipt = await orchestrator.StartAsync(request, CancellationToken.None);

        Assert.True(receipt.ReusedExistingExecution);
        Assert.Equal(RuntimeExecutionState.Starting, receipt.State);
        Assert.Equal(1, gateway.StartCount);
        Assert.Equal(1, gateway.CancelCount);
        Assert.Equal(gateway.OperationName, gateway.CancelledOperationName);
    }

    [Fact]
    public async Task CloudRunJobRuntimeRunOrchestrator_StartFailurePersistsTerminalFailure()
    {
        var request = Request("gateway-failure");
        var store = new InMemoryCloudRunExecutionStore();
        var gateway = new RecordingCloudRunGateway
        {
            StartException = new InvalidOperationException("provider rejected launch")
        };
        var orchestrator = CreateCloudRunOrchestrator(store, gateway);

        var receipt = await orchestrator.StartAsync(request, CancellationToken.None);
        var stored = await store.GetAsync(request.ExecutionId, CancellationToken.None);

        Assert.Equal(RuntimeExecutionState.Failed, receipt.State);
        Assert.Null(receipt.RejectionCode);
        Assert.Equal("provider rejected launch", receipt.Message);
        Assert.Equal(RuntimeExecutionState.Failed, stored!.State);
        Assert.Equal("cloud_run_job_start_failed", stored.FailureCode);
        Assert.Equal(1, gateway.StartCount);
    }

    [Fact]
    public async Task CloudRunJobRuntimeRunOrchestrator_StopHandlesMissingTerminalPendingAndCancellationFailure()
    {
        var store = new InMemoryCloudRunExecutionStore();
        var gateway = new RecordingCloudRunGateway();
        var orchestrator = CreateCloudRunOrchestrator(store, gateway);
        var missingId = new RuntimeExecutionId(Guid.NewGuid());

        var missing = await orchestrator.StopAsync(missingId, RuntimeStopReason.UserRequest, CancellationToken.None);

        var terminalRequest = Request("terminal");
        await store.UpdateAsync(Record(terminalRequest) with { State = RuntimeExecutionState.Succeeded }, CancellationToken.None);
        var terminal = await orchestrator.StopAsync(terminalRequest.ExecutionId, RuntimeStopReason.UserRequest, CancellationToken.None);

        var pendingRequest = Request("pending");
        await store.UpdateAsync(Record(pendingRequest) with { State = RuntimeExecutionState.Starting }, CancellationToken.None);
        var pending = await orchestrator.StopAsync(pendingRequest.ExecutionId, RuntimeStopReason.UserRequest, CancellationToken.None);

        var runningRequest = Request("running");
        await store.UpdateAsync(Record(runningRequest) with
        {
            State = RuntimeExecutionState.Running,
            ProviderOperationName = "projects/p/locations/europe-southwest1/operations/op-4",
            ProviderExecutionName = "projects/p/locations/europe-southwest1/jobs/natureprotector-simulator/executions/ex-4"
        }, CancellationToken.None);
        gateway.CancelException = new InvalidOperationException("cancel refused");
        var failedCancel = await orchestrator.StopAsync(runningRequest.ExecutionId, RuntimeStopReason.Timeout, CancellationToken.None);

        Assert.Equal(RuntimeExecutionState.Unknown, missing.State);
        Assert.False(missing.StopAccepted);
        Assert.Equal(RuntimeExecutionState.Succeeded, terminal.State);
        Assert.False(terminal.StopAccepted);
        Assert.Equal(RuntimeExecutionState.Starting, pending.State);
        Assert.False(pending.StopAccepted);
        Assert.Equal(RuntimeExecutionState.Running, failedCancel.State);
        Assert.False(failedCancel.StopAccepted);
        Assert.Equal("cancel refused", failedCancel.Message);
    }

    [Fact]
    public async Task MetadataGoogleAccessTokenSource_CachesTokenAndClassifiesHttpFailures()
    {
        var clock = new FakeTimeProvider(DateTimeOffset.Parse("2026-07-21T10:00:00Z", CultureInfo.InvariantCulture));
        var successHandler = new RecordingHttpHandler(new HttpResponseMessage(HttpStatusCode.OK)
        {
            Content = new StringContent("{\"access_token\":\"token-1\",\"expires_in\":300}", Encoding.UTF8, "application/json")
        });
        var source = new MetadataGoogleAccessTokenSource(
            new HttpClient(successHandler),
            NullLogger<MetadataGoogleAccessTokenSource>.Instance,
            clock);

        var first = await source.GetAccessTokenAsync(CancellationToken.None);
        var second = await source.GetAccessTokenAsync(CancellationToken.None);

        Assert.Equal("token-1", first);
        Assert.Equal("token-1", second);
        var tokenRequest = Assert.Single(successHandler.Requests);
        Assert.Equal("Google", tokenRequest.Headers.GetValues("Metadata-Flavor").Single());

        var failureSource = new MetadataGoogleAccessTokenSource(
            new HttpClient(new RecordingHttpHandler(new HttpResponseMessage(HttpStatusCode.ServiceUnavailable))),
            NullLogger<MetadataGoogleAccessTokenSource>.Instance,
            clock);

        var failure = await Assert.ThrowsAsync<CloudRunGatewayException>(() =>
            failureSource.GetAccessTokenAsync(CancellationToken.None));
        Assert.Equal("metadata_token_http_503", failure.Code);
        Assert.True(failure.IsRetryable);
    }

    [Fact]
    public async Task CloudRunJobsRestGateway_StartPostsBoundedJobRunPayloadWithCorrelationEnvironment()
    {
        var handler = new RecordingHttpHandler(new HttpResponseMessage(HttpStatusCode.OK)
        {
            Content = new StringContent(
                "{\"name\":\"projects/p/locations/europe-southwest1/operations/op-start\"}",
                Encoding.UTF8,
                "application/json")
        });
        var gateway = CreateRestGateway(handler);
        var request = Request("rest-start") with
        {
            Environment = "Evidence",
            Timeout = TimeSpan.FromSeconds(999),
            Simulation = new RuntimeSimulationParameters(
                "PT-11",
                "scenario_b",
                8,
                5,
                2,
                456,
                "legacy-noise",
                ["noise", "drift"],
                "corr-rest-start")
        };

        var operationName = await gateway.StartAsync(request, request.ExecutionId, CancellationToken.None);

        Assert.Equal("projects/p/locations/europe-southwest1/operations/op-start", operationName);
        var httpRequest = Assert.Single(handler.Requests);
        Assert.Equal(HttpMethod.Post, httpRequest.Method);
        Assert.Equal(
            "https://run.googleapis.com/v2/projects/p/locations/europe-southwest1/jobs/natureprotector-simulator:run",
            httpRequest.RequestUri!.ToString());
        Assert.Equal("Bearer fake-token", httpRequest.Headers.Authorization!.ToString());

        using var payload = JsonDocument.Parse(handler.Body!);
        var overrides = payload.RootElement.GetProperty("overrides");
        Assert.Equal(1, overrides.GetProperty("taskCount").GetInt32());
        Assert.Equal("120s", overrides.GetProperty("timeout").GetString());
        var env = overrides.GetProperty("containerOverrides")[0].GetProperty("env")
            .EnumerateArray()
            .ToDictionary(
                item => item.GetProperty("name").GetString()!,
                item => item.GetProperty("value").GetString()!,
                StringComparer.Ordinal);
        Assert.Equal("Evidence", env["DOTNET_ENVIRONMENT"]);
        Assert.Equal("PT-11", env["Simulator__ControlPlaneAreaCode"]);
        Assert.Equal("scenario_b", env["Simulator__ControlPlaneScenarioCode"]);
        Assert.Equal("corr-rest-start", env["Simulator__RunOverrides__OrchestratorCorrelationId"]);
        Assert.Equal("noise", env["Simulator__RunOverrides__DegradationProfiles__0"]);
        Assert.Equal("drift", env["Simulator__RunOverrides__DegradationProfiles__1"]);
    }

    [Fact]
    public async Task CloudRunJobsRestGateway_GetParsesDoneOperationAndSafeProviderFailure()
    {
        var handler = new QueueingHttpHandler(
            new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent(
                    """
                    {
                      "done": true,
                      "response": {
                        "name": "projects/p/locations/europe-southwest1/jobs/natureprotector-simulator/executions/ex-success",
                        "startTime": "2026-07-21T10:00:00Z",
                        "completionTime": "2026-07-21T10:00:10Z"
                      }
                    }
                    """,
                    Encoding.UTF8,
                    "application/json")
            },
            new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent(
                    """
                    {
                      "done": true,
                      "error": {
                        "code": 13,
                        "message": "failed with token=secret-value\nsecond line"
                      },
                      "metadata": {
                        "target": "projects/p/locations/europe-southwest1/jobs/natureprotector-simulator/executions/ex-failed",
                        "createTime": "2026-07-21T10:01:00Z",
                        "completionTime": "2026-07-21T10:01:20Z"
                      }
                    }
                    """,
                    Encoding.UTF8,
                    "application/json")
            });
        var gateway = CreateRestGateway(handler);

        var success = await gateway.GetAsync("projects/p/locations/europe-southwest1/operations/op-success", CancellationToken.None);
        var failure = await gateway.GetAsync("projects/p/locations/europe-southwest1/operations/op-failed", CancellationToken.None);

        Assert.True(success.Done);
        Assert.False(success.Failed);
        Assert.Equal("projects/p/locations/europe-southwest1/jobs/natureprotector-simulator/executions/ex-success", success.ExecutionName);
        Assert.Equal(DateTimeOffset.Parse("2026-07-21T10:00:00Z", CultureInfo.InvariantCulture), success.StartedAtUtc);
        Assert.Equal(DateTimeOffset.Parse("2026-07-21T10:00:10Z", CultureInfo.InvariantCulture), success.FinishedAtUtc);
        Assert.True(failure.Done);
        Assert.True(failure.Failed);
        Assert.Equal("13", failure.FailureCode);
        Assert.Contains("token=[REDACTED]", failure.FailureMessage, StringComparison.Ordinal);
        Assert.DoesNotContain("secret-value", failure.FailureMessage, StringComparison.Ordinal);
        Assert.Equal(2, handler.Requests.Count);
    }

    [Fact]
    public async Task CloudRunJobsRestGateway_CancelRequiresExecutionNameAndPostsCancelRequest()
    {
        var handler = new RecordingHttpHandler(new HttpResponseMessage(HttpStatusCode.OK));
        var gateway = CreateRestGateway(handler);

        var missing = await Assert.ThrowsAsync<CloudRunGatewayException>(() =>
            gateway.CancelAsync("projects/p/locations/europe-southwest1/operations/op-cancel", null, CancellationToken.None));
        await gateway.CancelAsync(
            "projects/p/locations/europe-southwest1/operations/op-cancel",
            "projects/p/locations/europe-southwest1/jobs/natureprotector-simulator/executions/ex-cancel",
            CancellationToken.None);

        Assert.Equal("cloud_run_execution_name_unavailable", missing.Code);
        Assert.True(missing.IsRetryable);
        var request = Assert.Single(handler.Requests);
        Assert.Equal(HttpMethod.Post, request.Method);
        Assert.Equal(
            "https://run.googleapis.com/v2/projects/p/locations/europe-southwest1/jobs/natureprotector-simulator/executions/ex-cancel:cancel",
            request.RequestUri!.ToString());
    }

    private static CloudRunJobRuntimeRunOrchestrator CreateCloudRunOrchestrator(
        ICloudRunExecutionStore store,
        ICloudRunJobsGateway gateway)
        => new(
            store,
            gateway,
            Options.Create(new RuntimeOrchestrationOptions
            {
                CloudRunProjectId = "p",
                CloudRunRegion = "europe-southwest1",
                CloudRunSimulatorJobName = "natureprotector-simulator",
                CloudRunLaunchLeaseSeconds = 5,
                CloudRunPollIntervalSeconds = 1
            }),
            NullLogger<CloudRunJobRuntimeRunOrchestrator>.Instance);

    private static CloudRunJobsRestGateway CreateRestGateway(HttpMessageHandler handler)
        => new(
            new HttpClient(handler),
            new StaticAccessTokenSource(),
            Options.Create(new RuntimeOrchestrationOptions
            {
                CloudRunProjectId = "p",
                CloudRunRegion = "europe-southwest1",
                CloudRunSimulatorJobName = "natureprotector-simulator",
                CloudRunSimulatorContainerName = "simulator",
                MaximumTimeoutSeconds = 120
            }),
            NullLogger<CloudRunJobsRestGateway>.Instance);

    private static RuntimeLaunchRequest Request(string idempotencyKey) => new(
        new RuntimeExecutionId(Guid.NewGuid()),
        Guid.NewGuid(),
        idempotencyKey,
        "local",
        RuntimeLaunchProfile.Simulation,
        new RuntimeSimulationParameters("PT-11", "scenario_a", 10, 3, 1, 123, null, ["none"], $"corr-{idempotencyKey}"),
        null,
        CollectEvidence: true,
        WaitForCompletion: false,
        TimeSpan.FromSeconds(30),
        new RuntimeEvidenceReference($"evidence-{idempotencyKey}", $"location-{idempotencyKey}"));

    private static CloudRunExecutionRecord Record(RuntimeLaunchRequest request)
    {
        var now = DateTimeOffset.Parse("2026-07-21T12:00:00Z", CultureInfo.InvariantCulture);
        return new CloudRunExecutionRecord(
            request.ExecutionId,
            request.IdempotencyKey,
            ProviderOperationName: null,
            ProviderExecutionName: null,
            State: RuntimeExecutionState.Starting,
            AcceptedAtUtc: now,
            UpdatedAtUtc: now,
            StartedAtUtc: null,
            FinishedAtUtc: null,
            FailureCode: null,
            FailureMessage: null,
            request.Simulation.OrchestratorCorrelationId,
            request.Evidence,
            Guid.NewGuid(),
            now.AddMinutes(1));
    }

    private sealed class InMemoryCloudRunExecutionStore : ICloudRunExecutionStore
    {
        private readonly Dictionary<RuntimeExecutionId, CloudRunExecutionRecord> _records = [];
        private readonly CloudRunExecutionReservation? _presetReservation;

        public InMemoryCloudRunExecutionStore()
        {
        }

        public InMemoryCloudRunExecutionStore(CloudRunExecutionReservation presetReservation)
        {
            _presetReservation = presetReservation;
            _records[presetReservation.Record.ExecutionId] = presetReservation.Record;
        }

        public bool AttachShouldSucceed { get; set; } = true;

        public Task<CloudRunExecutionReservation> ReserveAsync(
            RuntimeLaunchRequest request,
            TimeSpan leaseDuration,
            CancellationToken cancellationToken)
        {
            if (_presetReservation is not null)
            {
                return Task.FromResult(_presetReservation);
            }

            var leaseToken = Guid.NewGuid();
            var record = Record(request) with { LaunchLeaseToken = leaseToken };
            _records[request.ExecutionId] = record;
            return Task.FromResult(new CloudRunExecutionReservation(
                record,
                leaseToken,
                OwnsLaunch: true,
                ReusedExistingExecution: false));
        }

        public Task<bool> AttachOperationAsync(
            RuntimeExecutionId executionId,
            Guid leaseToken,
            string operationName,
            CancellationToken cancellationToken)
        {
            if (!AttachShouldSucceed)
            {
                return Task.FromResult(false);
            }

            var record = _records[executionId];
            _records[executionId] = record with
            {
                ProviderOperationName = operationName,
                State = RuntimeExecutionState.Running,
                LaunchLeaseToken = null,
                LaunchLeaseUntilUtc = null
            };
            return Task.FromResult(true);
        }

        public Task<CloudRunExecutionRecord?> GetAsync(RuntimeExecutionId executionId, CancellationToken cancellationToken)
            => Task.FromResult(_records.GetValueOrDefault(executionId));

        public Task UpdateAsync(CloudRunExecutionRecord record, CancellationToken cancellationToken)
        {
            _records[record.ExecutionId] = record;
            return Task.CompletedTask;
        }
    }

    private sealed class RecordingCloudRunGateway : ICloudRunJobsGateway
    {
        public string OperationName { get; set; } = "projects/p/locations/europe-southwest1/operations/op";
        public CloudRunOperationSnapshot Snapshot { get; set; } = new(
            "projects/p/locations/europe-southwest1/operations/op",
            null,
            Done: false,
            Failed: false,
            FailureCode: null,
            FailureMessage: null,
            StartedAtUtc: null,
            FinishedAtUtc: null);
        public Exception? StartException { get; set; }
        public Exception? CancelException { get; set; }
        public int StartCount { get; private set; }
        public int GetCount { get; private set; }
        public int CancelCount { get; private set; }
        public string? CancelledOperationName { get; private set; }

        public Task<string> StartAsync(
            RuntimeLaunchRequest request,
            RuntimeExecutionId executionId,
            CancellationToken cancellationToken)
        {
            StartCount++;
            if (StartException is not null)
            {
                throw StartException;
            }

            return Task.FromResult(OperationName);
        }

        public Task<CloudRunOperationSnapshot> GetAsync(string operationName, CancellationToken cancellationToken)
        {
            GetCount++;
            return Task.FromResult(Snapshot with { OperationName = operationName });
        }

        public Task CancelAsync(string operationName, string? executionName, CancellationToken cancellationToken)
        {
            CancelCount++;
            CancelledOperationName = operationName;
            if (CancelException is not null)
            {
                throw CancelException;
            }

            return Task.CompletedTask;
        }
    }

    private sealed class RecordingHttpHandler(HttpResponseMessage response) : HttpMessageHandler
    {
        public List<HttpRequestMessage> Requests { get; } = [];
        public string? Body { get; private set; }

        protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            Requests.Add(request);
            Body = request.Content is null ? null : await request.Content.ReadAsStringAsync(cancellationToken);
            return response;
        }
    }

    private sealed class QueueingHttpHandler(params HttpResponseMessage[] responses) : HttpMessageHandler
    {
        private readonly Queue<HttpResponseMessage> _responses = new(responses);
        public List<HttpRequestMessage> Requests { get; } = [];

        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            Requests.Add(request);
            return Task.FromResult(_responses.Dequeue());
        }
    }

    private sealed class StaticAccessTokenSource : IGoogleAccessTokenSource
    {
        public Task<string> GetAccessTokenAsync(CancellationToken cancellationToken) => Task.FromResult("fake-token");
    }

    private sealed class FakeTimeProvider(DateTimeOffset utcNow) : TimeProvider
    {
        public override DateTimeOffset GetUtcNow() => utcNow;
    }

    private sealed class TestHostEnvironment : IHostEnvironment
    {
        public string EnvironmentName { get; set; } = "Development";
        public string ApplicationName { get; set; } = "NatureProtector.Backoffice.Api.Tests";
        public string ContentRootPath { get; set; } = AppContext.BaseDirectory;
        public IFileProvider ContentRootFileProvider { get; set; } = new NullFileProvider();
    }
}
