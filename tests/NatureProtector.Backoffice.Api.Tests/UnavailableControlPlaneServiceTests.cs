using NatureProtector.Backoffice.Api.ControlPlane.Services;

namespace NatureProtector.Backoffice.Api.Tests;

public sealed class UnavailableControlPlaneServiceTests
{
    [Fact]
    public async Task UnavailableService_ReportsUnavailability_AndReturnsEmptyResults()
    {
        var service = new UnavailableControlPlaneService("Control plane disabled for tests.");

        Assert.False(service.IsAvailable);
        Assert.Equal("Control plane disabled for tests.", service.AvailabilityMessage);
        Assert.Empty(await service.ListConfigurationsAsync(CancellationToken.None));
        Assert.Null(await service.GetActiveConfigurationAsync(CancellationToken.None));
        Assert.Null(await service.ActivateConfigurationAsync(1, CancellationToken.None));
        Assert.Empty(await service.ListAreasAsync(null, CancellationToken.None));
        Assert.Null(await service.GetAreaAsync("proenca-a-nova", null, CancellationToken.None));
        Assert.Empty(await service.ListGridCellsAsync("proenca-a-nova", null, 0, 10, CancellationToken.None));
        Assert.Empty(await service.ListSensorNodesAsync("proenca-a-nova", null, 0, 10, CancellationToken.None));
        Assert.Empty(await service.ListScenariosAsync("proenca-a-nova", null, CancellationToken.None));
        Assert.Empty(await service.ListSimulationRunsAsync("proenca-a-nova", "scenario_a", null, 0, 10, CancellationToken.None));
        Assert.Null(await service.GetSimulationRunAsync(Guid.NewGuid(), CancellationToken.None));
        Assert.Null(await service.GetRuntimeRunAuditAsync(Guid.NewGuid(), CancellationToken.None));
        Assert.Null(await service.GetRuntimeRunTimingsAsync(Guid.NewGuid(), CancellationToken.None));
        Assert.Null(await service.GetAreaOperationalStateAsync("proenca-a-nova", null, CancellationToken.None));
        Assert.Empty(await service.ListCellOperationalStatesAsync("proenca-a-nova", null, 0, 10, CancellationToken.None));
        Assert.Empty(await service.ListActiveAlertsAsync("proenca-a-nova", null, CancellationToken.None));
        Assert.Null(await service.GetAreaGeoJSONAsync("proenca-a-nova", null, CancellationToken.None));

        var runtimeSummary = await service.GetRuntimeSummaryAsync("proenca-a-nova", 30, CancellationToken.None);
        Assert.Null(runtimeSummary.CurrentRun);
        Assert.Null(runtimeSummary.LatestRun);
        Assert.Equal(0, runtimeSummary.Pipeline.InboxTotal);
        Assert.NotEmpty(runtimeSummary.Limitations);

        var diagnostics = await service.ListRuntimeDiagnosticsAsync(CancellationToken.None);
        Assert.Empty(diagnostics.Diagnostics);
        Assert.Null(await service.ExecuteRuntimeDiagnosticAsync("anything", new(), CancellationToken.None));

        var start = await service.StartRuntimeRunAsync(
            new("proenca-a-nova", "scenario_b", null, null, null, null, null),
            CancellationToken.None);
        Assert.Equal("Unavailable", start.Status);
        Assert.Contains("disabled", start.Message, StringComparison.OrdinalIgnoreCase);

        var reset = await service.ResetRuntimeStateAsync(
            new("runtime", "RESET", DryRun: true),
            CancellationToken.None);
        Assert.Equal("Unavailable", reset.Status);
        Assert.True(reset.DryRun);
    }
}
