using System.Text.Json;
using System.Net;
using System.Net.Http.Json;

namespace NatureProtector.Backoffice.Api.Tests;

public sealed class ControlPlaneApiTests
{
    [Theory]
    [InlineData("/api/control/areas")]
    [InlineData("/api/control/areas/proenca-a-nova")]
    [InlineData("/api/control/areas/proenca-a-nova/grid-cells")]
    [InlineData("/api/control/areas/proenca-a-nova/sensor-nodes")]
    [InlineData("/api/control/areas/proenca-a-nova/scenarios")]
    [InlineData("/api/control/areas/proenca-a-nova/operational-state")]
    [InlineData("/api/control/areas/proenca-a-nova/cells/operational-state")]
    [InlineData("/api/control/areas/proenca-a-nova/alerts/active")]
    public async Task AreaEndpoints_ControlPlaneUnavailable_ReturnProblemDetails(string path)
    {
        const string availabilityMessage = "Control plane disabled for this test.";
        await using var factory = new ControlPlaneApiWebApplicationFactory(
            controlPlaneAvailable: false,
            availabilityMessage: availabilityMessage);
        using var client = factory.CreateClient();

        var response = await client.GetAsync(path);

        Assert.Equal(HttpStatusCode.ServiceUnavailable, response.StatusCode);
        await AssertUnavailableProblemDetailsAsync(response, availabilityMessage);
    }

    [Theory]
    [InlineData("/api/control/configurations")]
    [InlineData("/api/control/configurations/active")]
    public async Task ConfigurationGetEndpoints_ControlPlaneUnavailable_ReturnProblemDetails(string path)
    {
        const string availabilityMessage = "Control plane temporarily unavailable.";
        await using var factory = new ControlPlaneApiWebApplicationFactory(
            controlPlaneAvailable: false,
            availabilityMessage: availabilityMessage);
        using var client = factory.CreateClient();

        var response = await client.GetAsync(path);

        Assert.Equal(HttpStatusCode.ServiceUnavailable, response.StatusCode);
        await AssertUnavailableProblemDetailsAsync(response, availabilityMessage);
    }

    [Fact]
    public async Task ActivateConfiguration_ControlPlaneUnavailable_ReturnProblemDetails()
    {
        const string availabilityMessage = "Control plane unavailable during activation.";
        await using var factory = new ControlPlaneApiWebApplicationFactory(
            controlPlaneAvailable: false,
            availabilityMessage: availabilityMessage);
        using var client = factory.CreateClient();

        var response = await client.PostAsync("/api/control/configurations/1/activate", content: null);

        Assert.Equal(HttpStatusCode.ServiceUnavailable, response.StatusCode);
        await AssertUnavailableProblemDetailsAsync(response, availabilityMessage);
    }

    [Theory]
    [InlineData("/api/control/simulation-runs")]
    [InlineData("/api/control/simulation-runs/90000000-0000-0000-0000-000000000001")]
    [InlineData("/api/control/runtime/summary")]
    public async Task SimulationRunEndpoints_ControlPlaneUnavailable_ReturnProblemDetails(string path)
    {
        const string availabilityMessage = "Simulation run control plane unavailable.";
        await using var factory = new ControlPlaneApiWebApplicationFactory(
            controlPlaneAvailable: false,
            availabilityMessage: availabilityMessage);
        using var client = factory.CreateClient();

        var response = await client.GetAsync(path);

        Assert.Equal(HttpStatusCode.ServiceUnavailable, response.StatusCode);
        await AssertUnavailableProblemDetailsAsync(response, availabilityMessage);
    }

    [Fact]
    public async Task ListAreas_ExistingConfiguration_ReturnsSeededAreas()
    {
        await using var factory = new ControlPlaneApiWebApplicationFactory();
        using var client = factory.CreateClient();

        var response = await client.GetAsync("/api/control/areas?configurationVersion=1");

        response.EnsureSuccessStatusCode();
        using var document = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        var area = document.RootElement.EnumerateArray().Single();

        Assert.Equal("proenca-a-nova", area.GetProperty("code").GetString());
        Assert.Equal("Proenca-a-Nova", area.GetProperty("name").GetString());
        Assert.Equal("PT", area.GetProperty("countryCode").GetString());
        Assert.Equal(1, area.GetProperty("configurationVersionNumber").GetInt32());
        Assert.Equal(2, area.GetProperty("gridCellCount").GetInt32());
        Assert.Equal(2, area.GetProperty("sensorNodeCount").GetInt32());
        Assert.Equal(2, area.GetProperty("scenarioCount").GetInt32());
    }

    [Fact]
    public async Task ActiveConfigurationEndpoint_ReturnsCurrentControlPlaneState()
    {
        await using var factory = new ControlPlaneApiWebApplicationFactory();
        using var client = factory.CreateClient();

        var response = await client.GetAsync("/api/control/configurations/active");

        response.EnsureSuccessStatusCode();
        using var document = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        var root = document.RootElement;

        Assert.Equal(1, root.GetProperty("versionNumber").GetInt32());
        Assert.True(root.GetProperty("isActive").GetBoolean());
        Assert.Equal(1, root.GetProperty("areaCount").GetInt32());
        Assert.Equal(2, root.GetProperty("gridCellCount").GetInt32());
        Assert.Equal(2, root.GetProperty("sensorNodeCount").GetInt32());
        Assert.Equal(2, root.GetProperty("scenarioCount").GetInt32());
        Assert.Equal(1, root.GetProperty("simulationRunCount").GetInt32());
    }

    [Fact]
    public async Task ListConfigurations_SeededConfigurations_ReturnsConfigurationsDescending()
    {
        await using var factory = new ControlPlaneApiWebApplicationFactory();
        using var client = factory.CreateClient();

        var response = await client.GetAsync("/api/control/configurations");

        response.EnsureSuccessStatusCode();
        using var document = JsonDocument.Parse(await response.Content.ReadAsStringAsync());

        Assert.Equal(2, document.RootElement.GetArrayLength());
        Assert.Equal(2, document.RootElement[0].GetProperty("versionNumber").GetInt32());
        Assert.False(document.RootElement[0].GetProperty("isActive").GetBoolean());
        Assert.Equal(1, document.RootElement[1].GetProperty("versionNumber").GetInt32());
        Assert.True(document.RootElement[1].GetProperty("isActive").GetBoolean());
    }

    [Fact]
    public async Task AreaEndpoints_ExposePilotAreaTopology()
    {
        await using var factory = new ControlPlaneApiWebApplicationFactory();
        using var client = factory.CreateClient();

        var areaResponse = await client.GetAsync("/api/control/areas/proenca-a-nova");
        var scenariosResponse = await client.GetAsync("/api/control/areas/proenca-a-nova/scenarios");
        var sensorsResponse = await client.GetAsync("/api/control/areas/proenca-a-nova/sensor-nodes");
        var operationalStateResponse = await client.GetAsync("/api/control/areas/proenca-a-nova/operational-state");
        var cellStatesResponse = await client.GetAsync("/api/control/areas/proenca-a-nova/cells/operational-state");
        var alertsResponse = await client.GetAsync("/api/control/areas/proenca-a-nova/alerts/active");

        areaResponse.EnsureSuccessStatusCode();
        scenariosResponse.EnsureSuccessStatusCode();
        sensorsResponse.EnsureSuccessStatusCode();
        operationalStateResponse.EnsureSuccessStatusCode();
        cellStatesResponse.EnsureSuccessStatusCode();
        alertsResponse.EnsureSuccessStatusCode();

        using var areaDocument = JsonDocument.Parse(await areaResponse.Content.ReadAsStringAsync());
        using var scenariosDocument = JsonDocument.Parse(await scenariosResponse.Content.ReadAsStringAsync());
        using var sensorsDocument = JsonDocument.Parse(await sensorsResponse.Content.ReadAsStringAsync());
        using var stateDocument = JsonDocument.Parse(await operationalStateResponse.Content.ReadAsStringAsync());
        using var cellStatesDocument = JsonDocument.Parse(await cellStatesResponse.Content.ReadAsStringAsync());
        using var alertsDocument = JsonDocument.Parse(await alertsResponse.Content.ReadAsStringAsync());

        Assert.Equal("proenca-a-nova", areaDocument.RootElement.GetProperty("code").GetString());
        Assert.Equal(2, areaDocument.RootElement.GetProperty("gridCellCount").GetInt32());
        Assert.Equal(2, areaDocument.RootElement.GetProperty("sensorNodeCount").GetInt32());
        Assert.Equal(2, scenariosDocument.RootElement.GetArrayLength());
        Assert.Equal(2, sensorsDocument.RootElement.GetArrayLength());
        Assert.Equal("VeryHigh", stateDocument.RootElement.GetProperty("aggregateRiskLevel").GetString());
        Assert.Equal("Critical", stateDocument.RootElement.GetProperty("severity").GetString());
        Assert.Equal("Alarm", stateDocument.RootElement.GetProperty("alertState").GetString());
        Assert.Equal(2, cellStatesDocument.RootElement.GetArrayLength());
        Assert.Equal("PRO-001", cellStatesDocument.RootElement[0].GetProperty("cellCode").GetString());
        Assert.Equal(1, alertsDocument.RootElement.GetArrayLength());
        Assert.Equal("area-risk-high", alertsDocument.RootElement[0].GetProperty("alertCode").GetString());
        Assert.Equal("Alarm", alertsDocument.RootElement[0].GetProperty("alertState").GetString());
    }

    [Fact]
    public async Task GetArea_MissingArea_ReturnsNotFound()
    {
        await using var factory = new ControlPlaneApiWebApplicationFactory();
        using var client = factory.CreateClient();

        var response = await client.GetAsync("/api/control/areas/missing-area");

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    [Fact]
    public async Task ListGridCells_ExistingArea_ReturnsGridCellResponseFields()
    {
        await using var factory = new ControlPlaneApiWebApplicationFactory();
        using var client = factory.CreateClient();

        var response = await client.GetAsync("/api/control/areas/proenca-a-nova/grid-cells?skip=0&take=1");

        response.EnsureSuccessStatusCode();
        using var document = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        var cell = document.RootElement.EnumerateArray().Single();

        Assert.Equal("PRO-001", cell.GetProperty("cellCode").GetString());
        Assert.Equal(1, cell.GetProperty("configurationVersionNumber").GetInt32());
        Assert.Equal(39.75, cell.GetProperty("centroidLatitude").GetDouble());
        Assert.Equal(-7.90, cell.GetProperty("centroidLongitude").GetDouble());
        Assert.Equal(340.0, cell.GetProperty("altitudeMeters").GetDouble());
        Assert.Equal(7.5, cell.GetProperty("slopeDegrees").GetDouble());
        Assert.Equal(125.0, cell.GetProperty("aspectDegrees").GetDouble());
        Assert.Equal("forest", cell.GetProperty("landCoverClass").GetString());
        Assert.Equal(JsonValueKind.Null, cell.GetProperty("dominantForestType").ValueKind);
        Assert.Equal(JsonValueKind.Null, cell.GetProperty("dominantFuelModel").ValueKind);
        Assert.Equal(JsonValueKind.Null, cell.GetProperty("treeCoverDensity").ValueKind);
        Assert.Equal("high", cell.GetProperty("structuralHazard").GetString());
        Assert.Equal(JsonValueKind.Null, cell.GetProperty("conjuncturalHazard").ValueKind);
        Assert.Equal(1, cell.GetProperty("sensorNodeCount").GetInt32());
    }

    [Fact]
    public async Task SimulationRunsEndpoint_ReturnsSeededRun()
    {
        await using var factory = new ControlPlaneApiWebApplicationFactory();
        using var client = factory.CreateClient();

        var response = await client.GetAsync("/api/control/simulation-runs?areaCode=proenca-a-nova&scenarioCode=scenario_b");

        response.EnsureSuccessStatusCode();
        using var document = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        var run = document.RootElement.EnumerateArray().Single();

        Assert.Equal("proenca-a-nova", run.GetProperty("areaCode").GetString());
        Assert.Equal("scenario_b", run.GetProperty("scenarioCode").GetString());
        Assert.Equal("Completed", run.GetProperty("status").GetString());
        Assert.Equal(36, run.GetProperty("numberOfCycles").GetInt32());
    }

    [Fact]
    public async Task ListSimulationRuns_NoFilters_ReturnsSeededRun()
    {
        await using var factory = new ControlPlaneApiWebApplicationFactory();
        using var client = factory.CreateClient();

        var response = await client.GetAsync("/api/control/simulation-runs");

        response.EnsureSuccessStatusCode();
        using var document = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        var run = document.RootElement.EnumerateArray().Single();

        Assert.Equal(Guid.Parse("90000000-0000-0000-0000-000000000001"), run.GetProperty("id").GetGuid());
        Assert.Equal("proenca-a-nova", run.GetProperty("areaCode").GetString());
        Assert.Equal("scenario_b", run.GetProperty("scenarioCode").GetString());
    }

    [Fact]
    public async Task GetSimulationRun_ExistingRun_ReturnsSeededRun()
    {
        await using var factory = new ControlPlaneApiWebApplicationFactory();
        using var client = factory.CreateClient();

        var response = await client.GetAsync("/api/control/simulation-runs/90000000-0000-0000-0000-000000000001");

        response.EnsureSuccessStatusCode();
        using var document = JsonDocument.Parse(await response.Content.ReadAsStringAsync());

        Assert.Equal(Guid.Parse("90000000-0000-0000-0000-000000000001"), document.RootElement.GetProperty("id").GetGuid());
        Assert.Equal("Completed", document.RootElement.GetProperty("status").GetString());
        Assert.Equal(42, document.RootElement.GetProperty("executionSeed").GetInt32());
    }

    [Fact]
    public async Task RuntimeSummaryEndpoint_ReturnsAggregatedRuntimeState()
    {
        await using var factory = new ControlPlaneApiWebApplicationFactory();
        using var client = factory.CreateClient();

        var response = await client.GetAsync("/api/control/runtime/summary?areaCode=proenca-a-nova&recentMinutes=30");

        response.EnsureSuccessStatusCode();
        using var document = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        var root = document.RootElement;

        Assert.Equal("proenca-a-nova", root.GetProperty("areaCode").GetString());
        Assert.Equal(30, root.GetProperty("recentWindowMinutes").GetInt32());
        Assert.Equal("scenario_b", root.GetProperty("latestRun").GetProperty("scenarioCode").GetString());
        Assert.Equal(2, root.GetProperty("pipeline").GetProperty("inboxTotal").GetInt32());
        Assert.Equal(2, root.GetProperty("risk").GetProperty("recentCount").GetInt32());
        Assert.Equal("Alarm", root.GetProperty("areaOperationalState").GetProperty("alertState").GetString());
        Assert.Equal("VeryHigh", root.GetProperty("scoreComponents").GetProperty("npRiskClass").GetString());
        Assert.Equal("Moderate", root.GetProperty("indexComparison").GetProperty("fireWeatherIpmaClass").GetString());
        Assert.Equal("VeryLowDryness", root.GetProperty("indexComparison").GetProperty("kbdiDrynessClass").GetString());
        Assert.Equal("High", root.GetProperty("indexComparison").GetProperty("portugueseContextRiskProxyClass").GetString());
        Assert.Equal("NotAvailable", root.GetProperty("indexComparison").GetProperty("localFwiPercentileStatus").GetString());
        Assert.Equal(1, root.GetProperty("activeAlerts").GetArrayLength());
        Assert.NotEmpty(root.GetProperty("limitations").EnumerateArray());
    }

    [Fact]
    public async Task AreaGeoJsonEndpoint_ReturnsGeometry()
    {
        await using var factory = new ControlPlaneApiWebApplicationFactory();
        using var client = factory.CreateClient();

        var response = await client.GetAsync("/api/control/areas/proenca-a-nova/GeoJSON?configurationVersion=1");

        response.EnsureSuccessStatusCode();
        using var document = JsonDocument.Parse(await response.Content.ReadAsStringAsync());

        Assert.Equal(Guid.Parse("00000000-0000-0000-0000-000000000001"), document.RootElement.GetProperty("id").GetGuid());
        Assert.Equal("{\"type\":\"Polygon\",\"coordinates\":[]}", document.RootElement.GetProperty("geometryGeoJson").GetString());
    }

    [Fact]
    public async Task RuntimeDiagnosticsEndpoints_ReturnCatalogResultAndNotFound()
    {
        await using var factory = new ControlPlaneApiWebApplicationFactory();
        using var client = factory.CreateClient();

        var catalogResponse = await client.GetAsync("/api/control/runtime/diagnostics");
        var resultResponse = await client.PostAsync(
            "/api/control/runtime/diagnostics/runtime-table-counts",
            JsonContent.Create(new { areaCode = "proenca-a-nova", recentMinutes = 30 }));
        var missingResponse = await client.PostAsync(
            "/api/control/runtime/diagnostics/missing-diagnostic",
            JsonContent.Create(new { areaCode = "proenca-a-nova" }));

        catalogResponse.EnsureSuccessStatusCode();
        resultResponse.EnsureSuccessStatusCode();
        Assert.Equal(HttpStatusCode.NotFound, missingResponse.StatusCode);

        using var catalogDocument = JsonDocument.Parse(await catalogResponse.Content.ReadAsStringAsync());
        using var resultDocument = JsonDocument.Parse(await resultResponse.Content.ReadAsStringAsync());

        Assert.Equal("runtime-table-counts", catalogDocument.RootElement.GetProperty("diagnostics")[0].GetProperty("id").GetString());
        Assert.Equal("runtime-table-counts", resultDocument.RootElement.GetProperty("id").GetString());
        Assert.Equal("control", resultDocument.RootElement.GetProperty("rows")[0].GetProperty("schema").GetString());
    }

    [Fact]
    public async Task RuntimeRunAuditEndpoint_ReturnsBvCAndIndexContext()
    {
        await using var factory = new ControlPlaneApiWebApplicationFactory();
        using var client = factory.CreateClient();

        var response = await client.GetAsync("/api/control/runtime/runs/90000000-0000-0000-0000-000000000001/audit");

        response.EnsureSuccessStatusCode();
        using var document = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        var root = document.RootElement;

        Assert.Equal(72, root.GetProperty("expectedEvents").GetInt32());
        Assert.Equal(70, root.GetProperty("acceptedReadings").GetInt32());
        Assert.Equal(2, root.GetProperty("missingEvents").GetInt32());
        Assert.Equal("CompleteEligible", root.GetProperty("eligibilitySummary")[0].GetProperty("status").GetString());
    }

    [Fact]
    public async Task RuntimeRunStartAndReset_RespectDevelopmentAndReturnResponses()
    {
        await using var factory = new ControlPlaneApiWebApplicationFactory();
        using var client = factory.CreateClient();

        var startResponse = await client.PostAsync(
            "/api/control/runtime/runs",
            JsonContent.Create(new
            {
                areaCode = "proenca-a-nova",
                scenarioCode = "scenario_c",
                sensorCount = 6,
                numberOfCycles = 5,
                intervalSeconds = 30,
                seed = 42,
                degradationProfile = "missing-readings",
                degradationProfiles = new[] { "missing-readings" }
            }));
        var resetResponse = await client.PostAsync(
            "/api/control/runtime/reset",
            JsonContent.Create(new { scope = "runtime", confirm = "RESET", dryRun = true }));

        startResponse.EnsureSuccessStatusCode();
        resetResponse.EnsureSuccessStatusCode();

        using var startDocument = JsonDocument.Parse(await startResponse.Content.ReadAsStringAsync());
        using var resetDocument = JsonDocument.Parse(await resetResponse.Content.ReadAsStringAsync());

        Assert.Equal("Validated", startDocument.RootElement.GetProperty("status").GetString());
        Assert.Equal("DryRun", resetDocument.RootElement.GetProperty("status").GetString());
    }

    [Fact]
    public async Task ControlledValidationP3Availability_ControlPlaneUnavailable_ReturnsProblemDetails()
    {
        const string availabilityMessage = "Control plane unavailable for P3.";
        await using var factory = new ControlPlaneApiWebApplicationFactory(
            controlPlaneAvailable: false,
            availabilityMessage: availabilityMessage);
        using var client = factory.CreateClient();

        var response = await client.GetAsync("/api/dev/controlled-validation/p3");

        Assert.Equal(HttpStatusCode.ServiceUnavailable, response.StatusCode);
        await AssertUnavailableProblemDetailsAsync(response, availabilityMessage);
    }

    [Fact]
    public async Task ControlledValidationP3Run_Production_ReturnsForbidden()
    {
        await using var factory = new ControlPlaneApiWebApplicationFactory(environmentName: "Production");
        using var client = factory.CreateClient();

        var response = await client.PostAsync(
            "/api/dev/controlled-validation/p3/run",
            JsonContent.Create(new { runLabel = "controlled-validation-p3-negative-pipeline-tests" }));

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
        using var document = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        Assert.Equal("Production", document.RootElement.GetProperty("environment").GetString());
    }

    [Fact]
    public async Task ControlledValidationP3Run_Development_ReturnsStructuredResponse()
    {
        await using var factory = new ControlPlaneApiWebApplicationFactory();
        using var client = factory.CreateClient();

        var response = await client.PostAsync(
            "/api/dev/controlled-validation/p3/run",
            JsonContent.Create(new
            {
                runLabel = "controlled-validation-p3-negative-pipeline-tests",
                waitForCompletion = true,
                collectEvidence = true,
                runAuditAfterCompletion = false,
                timeoutSeconds = 300
            }));

        response.EnsureSuccessStatusCode();
        using var document = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        var root = document.RootElement;

        Assert.Equal("controlled-validation-p3-negative-pipeline-tests", root.GetProperty("runLabel").GetString());
        Assert.Equal("P3NegativePipeline", root.GetProperty("phase").GetString());
        Assert.True(root.GetProperty("auditRequired").GetBoolean());
        Assert.Equal(11, root.GetProperty("messageCount").GetInt32());
        Assert.Equal(10, root.GetProperty("executableCases").GetInt32());
        Assert.Equal(2, root.GetProperty("blockedCases").GetInt32());
    }

    [Fact]
    public async Task RuntimeRunTimingsEndpoint_ReturnsTimingSummary()
    {
        await using var factory = new ControlPlaneApiWebApplicationFactory();
        using var client = factory.CreateClient();

        var response = await client.GetAsync("/api/control/runtime/runs/90000000-0000-0000-0000-000000000001/timings");

        response.EnsureSuccessStatusCode();
        using var document = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        var root = document.RootElement;

        Assert.Equal(Guid.Parse("90000000-0000-0000-0000-000000000001"), root.GetProperty("simulationRunId").GetGuid());
        Assert.Equal(840_000, root.GetProperty("runDurationMs").GetDouble());
        Assert.Equal(2, root.GetProperty("attempts").GetProperty("attemptCount").GetInt32());
        Assert.NotEmpty(root.GetProperty("stages").EnumerateArray());
    }

    [Fact]
    public async Task RuntimeRunTimingsEndpoint_MissingRun_ReturnsNotFound()
    {
        await using var factory = new ControlPlaneApiWebApplicationFactory();
        using var client = factory.CreateClient();

        var response = await client.GetAsync($"/api/control/runtime/runs/{Guid.NewGuid()}/timings");

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    [Fact]
    public async Task GetSimulationRun_MissingRun_ReturnsNotFound()
    {
        await using var factory = new ControlPlaneApiWebApplicationFactory();
        using var client = factory.CreateClient();

        var response = await client.GetAsync($"/api/control/simulation-runs/{Guid.NewGuid()}");

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    [Fact]
    public async Task ActivateConfigurationEndpoint_SwitchesActiveVersion()
    {
        await using var factory = new ControlPlaneApiWebApplicationFactory();
        using var client = factory.CreateClient();

        var activateResponse = await client.PostAsync("/api/control/configurations/2/activate", content: null);
        var activeResponse = await client.GetAsync("/api/control/configurations/active");

        activateResponse.EnsureSuccessStatusCode();
        activeResponse.EnsureSuccessStatusCode();

        using var activateDocument = JsonDocument.Parse(await activateResponse.Content.ReadAsStringAsync());
        using var activeDocument = JsonDocument.Parse(await activeResponse.Content.ReadAsStringAsync());

        Assert.Equal(2, activateDocument.RootElement.GetProperty("versionNumber").GetInt32());
        Assert.True(activateDocument.RootElement.GetProperty("isActive").GetBoolean());
        Assert.Equal(2, activeDocument.RootElement.GetProperty("versionNumber").GetInt32());
        Assert.True(activeDocument.RootElement.GetProperty("isActive").GetBoolean());
    }

    [Fact]
    public async Task ActivateConfiguration_MissingVersion_ReturnsNotFound()
    {
        await using var factory = new ControlPlaneApiWebApplicationFactory();
        using var client = factory.CreateClient();

        var response = await client.PostAsync("/api/control/configurations/99/activate", content: null);

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    private static async Task AssertUnavailableProblemDetailsAsync(
        HttpResponseMessage response,
        string expectedDetail)
    {
        using var document = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        var root = document.RootElement;

        Assert.Equal("Control plane unavailable", root.GetProperty("title").GetString());
        Assert.Equal(expectedDetail, root.GetProperty("detail").GetString());
        Assert.Equal((int)HttpStatusCode.ServiceUnavailable, root.GetProperty("status").GetInt32());
        Assert.Equal(JsonValueKind.String, root.GetProperty("traceId").ValueKind);
    }
}
