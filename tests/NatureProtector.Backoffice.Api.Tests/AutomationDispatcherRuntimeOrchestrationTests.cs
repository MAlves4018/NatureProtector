using System.Globalization;
using System.Net;
using System.Text.Json;
using Microsoft.AspNetCore.Hosting;
using Microsoft.Extensions.FileProviders;
using Microsoft.Extensions.Options;
using NatureProtector.Backoffice.Api.Operations.Configuration;
using NatureProtector.Backoffice.Api.Operations.Services;
using NatureProtector.Backoffice.Api.RuntimeOrchestration;

namespace NatureProtector.Backoffice.Api.Tests;

public sealed class AutomationDispatcherRuntimeOrchestrationTests
{
    [Fact]
    public async Task SafeAutomationDispatcher_SimulationMode_RecordsRequestWithoutExternalDispatch()
    {
        var dispatcher = new SafeAutomationDispatcher(
            new SingleClientFactory(new HttpClient(new RecordingHandler(HttpStatusCode.Accepted))),
            Options.Create(new OperationsOptions { Mode = "Simulation" }),
            new TestWebHostEnvironment());
        var operation = Operation();

        var result = await dispatcher.DispatchAsync(Definition(), operation, CancellationToken.None);

        Assert.Equal("Queued", result.Status);
        Assert.Equal("simulation", result.Provider);
        Assert.Equal($"simulation://{operation.Id:N}", result.ProviderReference);
        Assert.Equal("DEMONSTRATION_ONLY", result.EvidenceLevel);
        Assert.Contains("no external workflow", result.Limitation);
    }

    [Fact]
    public async Task SafeAutomationDispatcher_DisabledMode_BlocksWithoutHttpRequest()
    {
        var handler = new RecordingHandler(HttpStatusCode.Accepted);
        var dispatcher = new SafeAutomationDispatcher(
            new SingleClientFactory(new HttpClient(handler)),
            Options.Create(new OperationsOptions { Mode = "Disabled" }),
            new TestWebHostEnvironment());

        var result = await dispatcher.DispatchAsync(Definition(), Operation(), CancellationToken.None);

        Assert.Equal("Blocked", result.Status);
        Assert.Equal("disabled", result.Provider);
        Assert.Null(result.ProviderReference);
        Assert.Equal("NOT_PROVED", result.EvidenceLevel);
        Assert.Equal(0, handler.RequestCount);
    }

    [Fact]
    public async Task SafeAutomationDispatcher_GitHubModeRequiresRepositoryAndServerSideToken()
    {
        var dispatcher = new SafeAutomationDispatcher(
            new SingleClientFactory(new HttpClient(new RecordingHandler(HttpStatusCode.Accepted))),
            Options.Create(new OperationsOptions { Mode = "GitHub", GitHubRepository = "owner/repo" }),
            new TestWebHostEnvironment());

        var result = await dispatcher.DispatchAsync(Definition(), Operation(), CancellationToken.None);

        Assert.Equal("Blocked", result.Status);
        Assert.Equal("github-actions", result.Provider);
        Assert.Contains("server-side automation token", result.Limitation);
    }

    [Fact]
    public async Task SafeAutomationDispatcher_GitHubMode_PostsClosedWorkflowInputs()
    {
        var handler = new RecordingHandler(HttpStatusCode.NoContent);
        using var client = new HttpClient(handler);
        var dispatcher = new SafeAutomationDispatcher(
            new SingleClientFactory(client),
            Options.Create(new OperationsOptions
            {
                Mode = "GitHub",
                GitHubApiBaseUrl = "https://api.github.test",
                GitHubRepository = "/owner/repo/",
                GitHubToken = "server-token"
            }),
            new TestWebHostEnvironment());
        var operation = Operation();

        var result = await dispatcher.DispatchAsync(Definition(), operation, CancellationToken.None);

        Assert.Equal("Queued", result.Status);
        Assert.Equal("github-actions", result.Provider);
        Assert.Equal("https://github.com/owner/repo/actions/workflows/workflow.yml", result.ProviderReference);
        Assert.Equal("IMPLEMENTED_NOT_PROVED", result.EvidenceLevel);
        Assert.Equal(HttpMethod.Post, handler.Request!.Method);
        Assert.Equal("https://api.github.test/repos/owner/repo/actions/workflows/workflow.yml/dispatches", handler.Request.RequestUri!.ToString());
        Assert.Equal("Bearer", handler.Request.Headers.Authorization!.Scheme);
        Assert.Equal("server-token", handler.Request.Headers.Authorization.Parameter);
        Assert.Contains("NatureProtector-Operations-Control-Plane", handler.Request.Headers.UserAgent.ToString());

        using var payload = JsonDocument.Parse(handler.Body!);
        Assert.Equal("refs/heads/test", payload.RootElement.GetProperty("ref").GetString());
        var inputs = payload.RootElement.GetProperty("inputs");
        Assert.Equal(operation.Id.ToString("D"), inputs.GetProperty("operation_id").GetString());
        Assert.Equal("quality-run", inputs.GetProperty("operation_kind").GetString());
        Assert.Equal("local", inputs.GetProperty("environment").GetString());
        Assert.Equal("true", inputs.GetProperty("collect_evidence").GetString());
        Assert.Equal("bar", inputs.GetProperty("foo").GetString());
    }

    [Fact]
    public async Task SafeAutomationDispatcher_GitHubMode_ReturnsBoundedFailureDetail()
    {
        var detail = new string('x', 400);
        var dispatcher = new SafeAutomationDispatcher(
            new SingleClientFactory(new HttpClient(new RecordingHandler(HttpStatusCode.BadRequest, detail))),
            Options.Create(new OperationsOptions
            {
                Mode = "GitHub",
                GitHubApiBaseUrl = "https://api.github.test",
                GitHubRepository = "owner/repo",
                GitHubToken = "server-token"
            }),
            new TestWebHostEnvironment());

        var result = await dispatcher.DispatchAsync(Definition(), Operation(), CancellationToken.None);

        Assert.Equal("Failed", result.Status);
        Assert.Equal("github-actions", result.Provider);
        Assert.Equal("NOT_PROVED", result.EvidenceLevel);
        Assert.Contains("HTTP 400", result.Limitation);
        Assert.DoesNotContain(new string('x', 301), result.Limitation);
    }

    [Fact]
    public async Task DisabledRuntimeRunOrchestrator_RejectsStartAndStopWithoutSnapshot()
    {
        var executionId = new RuntimeExecutionId(Guid.NewGuid());
        var evidence = new RuntimeEvidenceReference("evidence-1", "evidence/location");
        var request = new RuntimeLaunchRequest(
            executionId,
            Guid.NewGuid(),
            "idem",
            "local",
            RuntimeLaunchProfile.Simulation,
            new RuntimeSimulationParameters("PT-11", "scenario_a", 10, 3, 1, 123, null, ["none"], "corr-1"),
            null,
            true,
            true,
            TimeSpan.FromSeconds(30),
            evidence);

        var start = await DisabledRuntimeRunOrchestrator.Instance.StartAsync(request, CancellationToken.None);
        var snapshot = await DisabledRuntimeRunOrchestrator.Instance.GetAsync(executionId, CancellationToken.None);
        var stop = await DisabledRuntimeRunOrchestrator.Instance.StopAsync(
            executionId,
            RuntimeStopReason.UserRequest,
            CancellationToken.None);

        Assert.False(DisabledRuntimeRunOrchestrator.Instance.IsAvailable);
        Assert.Equal("disabled", DisabledRuntimeRunOrchestrator.Instance.Provider);
        Assert.Equal(RuntimeExecutionState.Rejected, start.State);
        Assert.Equal("orchestration_disabled", start.RejectionCode);
        Assert.Equal("corr-1", start.LogCorrelation);
        Assert.Same(evidence, start.Evidence);
        Assert.Null(snapshot);
        Assert.Equal(RuntimeExecutionState.Rejected, stop.State);
        Assert.False(stop.StopAccepted);
        Assert.Contains("disabled", stop.Message);
    }

    [Fact]
    public async Task NullRuntimeEvidenceSink_RefusesCreationButIgnoresWrites()
    {
        var sink = NullRuntimeEvidenceSink.Instance;
        var evidence = new RuntimeEvidenceReference("evidence-1", "memory");

        Assert.False(sink.IsAvailable);
        var error = await Assert.ThrowsAsync<InvalidOperationException>(() =>
            sink.CreateAsync("runtime", DateTimeOffset.UtcNow, "label", CancellationToken.None));
        Assert.Equal(sink.AvailabilityMessage, error.Message);
        await sink.WriteJsonAsync(evidence, "sample.json", new { value = 1 }, CancellationToken.None);
        await sink.WriteTextAsync(evidence, "sample.txt", "value", CancellationToken.None);
    }

    [Fact]
    public async Task FileSystemOperationStore_RoundTripsOverwritesFiltersAndOrdersRecords()
    {
        var root = Path.Combine(Path.GetTempPath(), "np-operation-store-tests", Guid.NewGuid().ToString("N"));
        try
        {
            var store = new FileSystemOperationStore(
                new TestWebHostEnvironment(),
                Options.Create(new OperationsOptions { StoreRoot = root }));
            var older = Operation(
                Guid.Parse("11111111-1111-1111-1111-111111111111"),
                "quality",
                DateTimeOffset.Parse("2026-07-21T10:00:00Z", CultureInfo.InvariantCulture));
            var newer = Operation(
                Guid.Parse("22222222-2222-2222-2222-222222222222"),
                "deployment",
                DateTimeOffset.Parse("2026-07-21T11:00:00Z", CultureInfo.InvariantCulture));

            await store.SaveAsync(older, CancellationToken.None);
            await store.SaveAsync(newer, CancellationToken.None);
            newer.Status = "Completed";
            newer.Provider = "github-actions";
            await store.SaveAsync(newer, CancellationToken.None);

            var missing = await store.GetAsync(Guid.Parse("33333333-3333-3333-3333-333333333333"), CancellationToken.None);
            var reloaded = await store.GetAsync(newer.Id, CancellationToken.None);
            var all = await store.ListAsync(null, 10, CancellationToken.None);
            var quality = await store.ListAsync("quality", 10, CancellationToken.None);
            var minimumTake = await store.ListAsync(null, 0, CancellationToken.None);

            Assert.Null(missing);
            Assert.NotNull(reloaded);
            Assert.Equal("Completed", reloaded.Status);
            Assert.Equal("github-actions", reloaded.Provider);
            Assert.Equal([newer.Id, older.Id], all.Select(record => record.Id).ToArray());
            Assert.Single(quality);
            Assert.Equal(older.Id, quality[0].Id);
            Assert.Single(minimumTake);
            Assert.Equal(newer.Id, minimumTake[0].Id);
        }
        finally
        {
            if (Directory.Exists(root))
            {
                Directory.Delete(root, recursive: true);
            }
        }
    }

    private static OperationDefinition Definition() => new(
        "quality-run",
        "quality",
        "Quality run",
        "Runs quality",
        "QualityExecute",
        "low",
        false,
        false,
        ["local"],
        [],
        "workflow.yml",
        string.Empty);

    private static EngineeringOperationRecord Operation() => Operation(
        Guid.Parse("aaaaaaaa-bbbb-cccc-dddd-eeeeeeeeeeee"),
        "quality",
        DateTimeOffset.Parse("2026-07-21T12:00:00Z", CultureInfo.InvariantCulture));

    private static EngineeringOperationRecord Operation(Guid id, string category, DateTimeOffset requestedAt) => new()
    {
        Id = id,
        OperationId = "quality-run",
        Category = category,
        DisplayName = "Quality run",
        Environment = "local",
        Ref = "refs/heads/test",
        RequestedBy = "operator",
        RequestedAt = requestedAt,
        UpdatedAt = requestedAt,
        CollectEvidence = true,
        RiskLevel = "low",
        Inputs = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
        {
            ["foo"] = "bar"
        }
    };

    private sealed class SingleClientFactory(HttpClient client) : IHttpClientFactory
    {
        public HttpClient CreateClient(string name) => client;
    }

    private sealed class RecordingHandler(HttpStatusCode statusCode, string response = "") : HttpMessageHandler
    {
        public HttpRequestMessage? Request { get; private set; }
        public string? Body { get; private set; }
        public int RequestCount { get; private set; }

        protected override async Task<HttpResponseMessage> SendAsync(
            HttpRequestMessage request,
            CancellationToken cancellationToken)
        {
            RequestCount++;
            Request = request;
            Body = request.Content is null ? null : await request.Content.ReadAsStringAsync(cancellationToken);
            return new HttpResponseMessage(statusCode)
            {
                Content = new StringContent(response)
            };
        }
    }

    private sealed class TestWebHostEnvironment : IWebHostEnvironment
    {
        public string EnvironmentName { get; set; } = "Development";
        public string ApplicationName { get; set; } = "NatureProtector.Backoffice.Api.Tests";
        public string WebRootPath { get; set; } = AppContext.BaseDirectory;
        public IFileProvider WebRootFileProvider { get; set; } = new NullFileProvider();
        public string ContentRootPath { get; set; } = AppContext.BaseDirectory;
        public IFileProvider ContentRootFileProvider { get; set; } = new NullFileProvider();
    }
}
