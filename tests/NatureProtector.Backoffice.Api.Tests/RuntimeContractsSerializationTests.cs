using System.Text.Json;
using NatureProtector.Backoffice.Api.ControlPlane.Contracts;

namespace NatureProtector.Backoffice.Api.Tests;

public sealed class RuntimeContractsSerializationTests
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);

    [Fact]
    public void RuntimeDiagnosticResultResponse_SerializesRowsAndLimitations()
    {
        var response = new RuntimeDiagnosticResultResponse(
            "latest-run-degradation-effects",
            "Latest run degradation effects",
            "Requested/resolved/applied/observed profile effects.",
            ["profile", "status", "appliedCount"],
            [new Dictionary<string, string?> { ["profile"] = "noise", ["status"] = "profile_inactive", ["appliedCount"] = "0" }],
            ["natural_variation_is_not_injected_noise"]);

        var json = JsonSerializer.Serialize(response, JsonOptions);
        var roundTrip = JsonSerializer.Deserialize<RuntimeDiagnosticResultResponse>(json, JsonOptions);

        Assert.NotNull(roundTrip);
        Assert.Equal(response.Id, roundTrip!.Id);
        Assert.Equal("profile", roundTrip.Columns[0]);
        Assert.Equal("noise", roundTrip.Rows[0]["profile"]);
        Assert.Equal("natural_variation_is_not_injected_noise", roundTrip.Limitations[0]);
    }

    [Fact]
    public void RuntimeProcessingAttemptResponse_SerializesNullableErrorFields()
    {
        var response = new RuntimeProcessingAttemptResponse(
            Guid.Parse("10000000-0000-0000-0000-000000000001"),
            Guid.Parse("20000000-0000-0000-0000-000000000001"),
            2,
            "reading_risk_pipeline",
            new DateTimeOffset(2026, 5, 30, 10, 0, 0, TimeSpan.Zero),
            null,
            "Processing",
            null,
            null);

        var json = JsonSerializer.Serialize(response, JsonOptions);
        var roundTrip = JsonSerializer.Deserialize<RuntimeProcessingAttemptResponse>(json, JsonOptions);

        Assert.NotNull(roundTrip);
        Assert.Equal("reading_risk_pipeline", roundTrip!.Stage);
        Assert.Null(roundTrip.FinishedAt);
        Assert.Null(roundTrip.ErrorCode);
        Assert.Null(roundTrip.ErrorMessage);
    }

    [Fact]
    public void RuntimeRejectedAndQuarantinedResponses_PreserveMetadataJson()
    {
        var rejected = new RuntimeRejectedEventResponse(
            Guid.Parse("30000000-0000-0000-0000-000000000001"),
            null,
            "missing_payload",
            "Payload was absent.",
            new DateTimeOffset(2026, 5, 30, 10, 1, 0, TimeSpan.Zero),
            "{\"stage\":\"consumer\"}");
        var quarantined = new RuntimeQuarantinedEventResponse(
            Guid.Parse("40000000-0000-0000-0000-000000000001"),
            Guid.Parse("50000000-0000-0000-0000-000000000001"),
            3,
            "permanent_failure",
            "Database data exception.",
            new DateTimeOffset(2026, 5, 30, 10, 2, 0, TimeSpan.Zero),
            "{\"stage\":\"reading_risk_pipeline\",\"sqlState\":\"22001\"}");

        var rejectedRoundTrip = JsonSerializer.Deserialize<RuntimeRejectedEventResponse>(
            JsonSerializer.Serialize(rejected, JsonOptions),
            JsonOptions);
        var quarantinedRoundTrip = JsonSerializer.Deserialize<RuntimeQuarantinedEventResponse>(
            JsonSerializer.Serialize(quarantined, JsonOptions),
            JsonOptions);

        Assert.NotNull(rejectedRoundTrip);
        Assert.NotNull(quarantinedRoundTrip);
        Assert.Null(rejectedRoundTrip!.EventId);
        Assert.Contains("missing_payload", rejectedRoundTrip.RejectionCode);
        Assert.Contains("22001", quarantinedRoundTrip!.MetadataJson);
    }

    [Fact]
    public void RuntimeRunStartAndResetResponses_PreserveOperationalFields()
    {
        var requested = new RuntimeRunOverrideValuesResponse(
            SensorCount: 6,
            NumberOfCycles: 5,
            IntervalSeconds: 30,
            Seed: 42,
            DegradationProfile: "missing-readings+noise",
            OrchestratorCorrelationId: "corr-1",
            DegradationProfiles: ["missing-readings", "noise"]);
        var start = new RuntimeRunStartResponse(
            Guid.Parse("60000000-0000-0000-0000-000000000001"),
            "corr-1",
            "Validated",
            "Run accepted.",
            new DateTimeOffset(2026, 5, 30, 10, 3, 0, TimeSpan.Zero),
            requested,
            null,
            ["dry_run_only"],
            "logs/runtime",
            "docs/evidence/runs/run-1");
        var reset = new RuntimeResetResponse(
            new DateTimeOffset(2026, 5, 30, 10, 4, 0, TimeSpan.Zero),
            DryRun: true,
            Status: "DryRun",
            Message: "No state was deleted.",
            Before: [new RuntimeTableCountResponse("pipeline", "event_inbox", 30)],
            After: [new RuntimeTableCountResponse("pipeline", "event_inbox", 30)]);

        var startRoundTrip = JsonSerializer.Deserialize<RuntimeRunStartResponse>(
            JsonSerializer.Serialize(start, JsonOptions),
            JsonOptions);
        var resetRoundTrip = JsonSerializer.Deserialize<RuntimeResetResponse>(
            JsonSerializer.Serialize(reset, JsonOptions),
            JsonOptions);

        Assert.NotNull(startRoundTrip);
        Assert.Equal("noise", startRoundTrip!.Requested.DegradationProfiles![1]);
        Assert.Equal("docs/evidence/runs/run-1", startRoundTrip.EvidenceDirectory);
        Assert.NotNull(resetRoundTrip);
        Assert.True(resetRoundTrip!.DryRun);
        Assert.Equal(30, resetRoundTrip.Before[0].Count);
    }

    [Fact]
    public void ControlledValidationP3Contracts_PreserveUiFields()
    {
        var request = new ControlledValidationP3RunRequest(
            "controlled-validation-p3-negative-pipeline-tests",
            WaitForCompletion: true,
            CollectEvidence: true,
            RunAuditAfterCompletion: false,
            TimeoutSeconds: 300);
        var response = new ControlledValidationP3RunResponse(
            Guid.Parse("70000000-0000-0000-0000-000000000001"),
            request.RunLabel!,
            "P3NegativePipeline",
            "Validated",
            "Development",
            "P3 request is valid.",
            new DateTimeOffset(2026, 6, 5, 16, 0, 0, TimeSpan.Zero),
            MessageCount: 11,
            ExecutableCases: 10,
            BlockedCases: 2,
            EvidencePath: "docs/evidence/controlled-validation/p3/run",
            QueryPackPath: null,
            AuditRequired: true,
            Run: null,
            Notes: ["query_pack_manual"]);

        var requestRoundTrip = JsonSerializer.Deserialize<ControlledValidationP3RunRequest>(
            JsonSerializer.Serialize(request, JsonOptions),
            JsonOptions);
        var responseRoundTrip = JsonSerializer.Deserialize<ControlledValidationP3RunResponse>(
            JsonSerializer.Serialize(response, JsonOptions),
            JsonOptions);

        Assert.NotNull(requestRoundTrip);
        Assert.Equal(300, requestRoundTrip!.TimeoutSeconds);
        Assert.NotNull(responseRoundTrip);
        Assert.Equal("P3NegativePipeline", responseRoundTrip!.Phase);
        Assert.True(responseRoundTrip.AuditRequired);
        Assert.Null(responseRoundTrip.QueryPackPath);
        Assert.Equal("query_pack_manual", responseRoundTrip.Notes[0]);
    }
}
