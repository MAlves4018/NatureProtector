using System.Text.Json;

namespace NatureProtector.Backoffice.Api.Tests;

public sealed class ControlPlaneApiTests
{
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
        Assert.Equal(2, cellStatesDocument.RootElement.GetArrayLength());
        Assert.Equal("PRO-001", cellStatesDocument.RootElement[0].GetProperty("cellCode").GetString());
        Assert.Equal(1, alertsDocument.RootElement.GetArrayLength());
        Assert.Equal("area-risk-high", alertsDocument.RootElement[0].GetProperty("alertCode").GetString());
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
}
