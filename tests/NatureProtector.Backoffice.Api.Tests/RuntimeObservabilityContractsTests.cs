using System.Text.Json;
using NatureProtector.Backoffice.Api.ControlPlane.Contracts;

namespace NatureProtector.Backoffice.Api.Tests;

public sealed class RuntimeObservabilityContractsTests
{
    [Fact]
    public void RuntimeObservabilityContracts_SerializeRunScopeEvidenceAndTimelineFields()
    {
        var observedAt = new DateTimeOffset(2026, 7, 21, 12, 0, 0, TimeSpan.Zero);
        var requestedRunId = Guid.Parse("aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaaaaa");
        var resolvedRunId = Guid.Parse("bbbbbbbb-bbbb-bbbb-bbbb-bbbbbbbbbbbb");
        var eventId = Guid.Parse("cccccccc-cccc-cccc-cccc-cccccccccccc");
        var limitation = new RuntimeLimitationResponse(
            "runtime_scope_partial",
            "Only run-scoped samples are included.");
        var scope = new RuntimeDataScopeResponse(
            requestedRunId,
            resolvedRunId,
            resolvedRunId,
            observedAt,
            "PostgreSQL",
            "simulation-run",
            [limitation]);
        var evidence = new RuntimeEvidenceItemResponse(
            "evidence-1",
            "Run evidence",
            "csv",
            observedAt,
            "local",
            "simulation-run",
            "v1",
            ContentAvailable: true,
            DownloadAvailable: true,
            Size: 42,
            Status: "Available",
            Limitation: null);
        var timeline = new RuntimeTimelinePointResponse(
            "PublishedAt",
            observedAt,
            "pipeline.event_inbox",
            "event",
            eventId,
            "measured");

        var json = JsonSerializer.Serialize(new { scope, evidence, timeline });

        Assert.Contains("\"RequestedRunId\":\"aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaaaaa\"", json, StringComparison.Ordinal);
        Assert.Contains("\"ResolvedRunId\":\"bbbbbbbb-bbbb-bbbb-bbbb-bbbbbbbbbbbb\"", json, StringComparison.Ordinal);
        Assert.Contains("\"DataRunId\":\"bbbbbbbb-bbbb-bbbb-bbbb-bbbbbbbbbbbb\"", json, StringComparison.Ordinal);
        Assert.Contains("\"Limitations\":[{", json, StringComparison.Ordinal);
        Assert.Contains("\"EvidenceId\":\"evidence-1\"", json, StringComparison.Ordinal);
        Assert.Contains("\"DownloadAvailable\":true", json, StringComparison.Ordinal);
        Assert.Contains("\"EventId\":\"cccccccc-cccc-cccc-cccc-cccccccccccc\"", json, StringComparison.Ordinal);
        Assert.Contains("\"Status\":\"measured\"", json, StringComparison.Ordinal);
    }

    [Fact]
    public void RuntimeObservabilityContracts_PreserveNullOptionalEvidenceAndTimelineFields()
    {
        var observedAt = new DateTimeOffset(2026, 7, 21, 12, 0, 0, TimeSpan.Zero);
        var scope = new RuntimeDataScopeResponse(
            Guid.NewGuid(),
            ResolvedRunId: null,
            DataRunId: null,
            observedAt,
            "none",
            "unresolved",
            []);
        var evidence = new RuntimeEvidenceItemResponse(
            "evidence-2",
            "Historical evidence",
            "md",
            GeneratedAt: null,
            "repository",
            "historical",
            Version: null,
            ContentAvailable: false,
            DownloadAvailable: false,
            Size: 0,
            Status: "Unavailable",
            Limitation: "No live content is attached.");
        var timeline = new RuntimeTimelinePointResponse(
            "SystemCompletedAt",
            observedAt,
            "control.runtime_orchestrator_executions",
            "run");

        Assert.Null(scope.ResolvedRunId);
        Assert.Null(scope.DataRunId);
        Assert.Empty(scope.Limitations);
        Assert.Null(evidence.GeneratedAt);
        Assert.Null(evidence.Version);
        Assert.False(evidence.ContentAvailable);
        Assert.False(evidence.DownloadAvailable);
        Assert.Equal("No live content is attached.", evidence.Limitation);
        Assert.Null(timeline.EventId);
        Assert.Null(timeline.Status);
    }
}
