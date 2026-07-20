using Microsoft.EntityFrameworkCore;
using NatureProtector.Backoffice.Api.ControlPlane.DataExplorer.Contracts;
using NatureProtector.Infrastructure.Postgres.Persistence;

namespace NatureProtector.Backoffice.Api.ControlPlane.DataExplorer.Services;

public sealed class EfReadOnlyDataExplorerService : IReadOnlyDataExplorerService
{
    private readonly IDbContextFactory<NatureProtectorControlDbContext> _dbContextFactory;

    public EfReadOnlyDataExplorerService(IDbContextFactory<NatureProtectorControlDbContext> dbContextFactory)
    {
        _dbContextFactory = dbContextFactory;
    }

    public Task<IReadOnlyList<DatasetDefinition>> ListDatasetsAsync(CancellationToken cancellationToken)
    {
        var datasets = new List<DatasetDefinition>
        {
            new("simulation-runs", "Simulation Runs", "Persisted simulation runs with status and timestamps.",
                ["Id", "ScenarioCode", "ScenarioName", "Status", "StartedAt", "EndedAt", "IntervalSeconds", "NumberOfCycles"], 500, 10000),
            new("runtime-operations", "Runtime Operations", "Runtime operation records with execution state.",
                ["OperationId", "State", "TerminalOutcome", "StartedAt", "UpdatedAt", "Provider"], 500, 10000),
            new("areas", "Areas", "Registered geographical areas.",
                ["Id", "Code", "Name", "CountryCode"], 500, 10000),
            new("sensor-nodes", "Sensor Nodes", "Registered sensor nodes per area.",
                ["Id", "AreaId", "Name", "Type", "IsActive", "Latitude", "Longitude"], 500, 10000),
            new("alerts", "Alerts", "Active and historical alert states.",
                ["Id", "AreaId", "AlertCode", "Severity", "Status", "TriggeredAt", "ResolvedAt"], 500, 10000),
            new("grid-cells", "Grid Cells", "Grid cell definitions per area.",
                ["Id", "AreaId", "CellCode", "CentroidLatitude", "CentroidLongitude"], 500, 10000),
        };
        return Task.FromResult<IReadOnlyList<DatasetDefinition>>(datasets);
    }

    public async Task<DataExplorerQueryResponse?> QueryAsync(DataExplorerQueryRequest request, CancellationToken cancellationToken)
    {
        var datasets = await ListDatasetsAsync(cancellationToken);
        var dataset = datasets.FirstOrDefault(d => d.DatasetId == request.DatasetId);
        if (dataset is null)
        {
            return null;
        }

        var limit = Math.Clamp(request.Limit, 1, dataset.MaxLimit);
        var offset = Math.Clamp(request.Offset, 0, dataset.MaxOffset);
        var limitations = new List<string>();

        await using var dbContext = await _dbContextFactory.CreateDbContextAsync(cancellationToken);

        var result = request.DatasetId switch
        {
            "simulation-runs" => await QuerySimulationRunsAsync(dbContext, limit, offset, cancellationToken),
            "runtime-operations" => await QueryRuntimeOperationsAsync(dbContext, limit, offset, cancellationToken),
            "areas" => await QueryAreasAsync(dbContext, limit, offset, cancellationToken),
            "sensor-nodes" => await QuerySensorNodesAsync(dbContext, limit, offset, cancellationToken),
            "alerts" => await QueryAlertsAsync(dbContext, limit, offset, cancellationToken),
            "grid-cells" => await QueryGridCellsAsync(dbContext, limit, offset, cancellationToken),
            _ => (Columns: new List<string>(), Rows: new List<IReadOnlyDictionary<string, string?>>(), TotalCount: 0),
        };

        if (request.Offset > dataset.MaxOffset)
        {
            limitations.Add($"Offset clamped to maximum allowed value ({dataset.MaxOffset}).");
        }
        if (result.TotalCount > limit)
        {
            limitations.Add($"Showing {limit} of {result.TotalCount} rows.");
        }

        return new DataExplorerQueryResponse(
            dataset.DatasetId,
            dataset.DisplayName,
            result.Columns,
            result.Rows,
            result.TotalCount,
            limit,
            offset,
            limitations);
    }

    private static async Task<(List<string> Columns, List<IReadOnlyDictionary<string, string?>> Rows, int TotalCount)> QuerySimulationRunsAsync(
        NatureProtectorControlDbContext dbContext, int limit, int offset, CancellationToken ct)
    {
        var query = dbContext.SimulationRuns.AsNoTracking().OrderByDescending(r => r.StartedAt);
        var total = await query.CountAsync(ct);
        var items = await query.Skip(offset).Take(limit).ToListAsync(ct);
        var columns = new List<string> { "Id", "ScenarioCode", "ScenarioName", "Status", "StartedAt", "EndedAt", "IntervalSeconds", "NumberOfCycles" };
        var rows = items.Select(r => (IReadOnlyDictionary<string, string?>)new Dictionary<string, string?>
        {
            ["Id"] = r.Id.ToString(),
            ["ScenarioCode"] = r.ScenarioCode,
            ["ScenarioName"] = r.ScenarioName,
            ["Status"] = r.Status.ToString(),
            ["StartedAt"] = r.StartedAt?.ToString("o"),
            ["EndedAt"] = r.EndedAt?.ToString("o"),
            ["IntervalSeconds"] = r.IntervalSeconds.ToString(),
            ["NumberOfCycles"] = r.NumberOfCycles.ToString(),
        }).ToList();
        return (columns, rows, total);
    }

    private static async Task<(List<string> Columns, List<IReadOnlyDictionary<string, string?>> Rows, int TotalCount)> QueryRuntimeOperationsAsync(
        NatureProtectorControlDbContext dbContext, int limit, int offset, CancellationToken ct)
    {
        var query = dbContext.RuntimeOperations.AsNoTracking().OrderByDescending(o => o.StartedAt);
        var total = await query.CountAsync(ct);
        var items = await query.Skip(offset).Take(limit).ToListAsync(ct);
        var columns = new List<string> { "OperationId", "State", "TerminalOutcome", "StartedAt", "UpdatedAt", "Provider" };
        var rows = items.Select(o => (IReadOnlyDictionary<string, string?>)new Dictionary<string, string?>
        {
            ["OperationId"] = o.OperationId.ToString(),
            ["State"] = o.State,
            ["TerminalOutcome"] = o.TerminalOutcome,
            ["StartedAt"] = o.StartedAt?.ToString("o"),
            ["UpdatedAt"] = o.UpdatedAt.ToString("o"),
            ["Provider"] = o.Provider,
        }).ToList();
        return (columns, rows, total);
    }

    private static async Task<(List<string> Columns, List<IReadOnlyDictionary<string, string?>> Rows, int TotalCount)> QueryAreasAsync(
        NatureProtectorControlDbContext dbContext, int limit, int offset, CancellationToken ct)
    {
        var query = dbContext.Areas.AsNoTracking().OrderBy(a => a.Code);
        var total = await query.CountAsync(ct);
        var items = await query.Skip(offset).Take(limit).ToListAsync(ct);
        var columns = new List<string> { "Id", "Code", "Name", "CountryCode" };
        var rows = items.Select(a => (IReadOnlyDictionary<string, string?>)new Dictionary<string, string?>
        {
            ["Id"] = a.Id.ToString(),
            ["Code"] = a.Code,
            ["Name"] = a.Name,
            ["CountryCode"] = a.CountryCode,
        }).ToList();
        return (columns, rows, total);
    }

    private static async Task<(List<string> Columns, List<IReadOnlyDictionary<string, string?>> Rows, int TotalCount)> QuerySensorNodesAsync(
        NatureProtectorControlDbContext dbContext, int limit, int offset, CancellationToken ct)
    {
        var query = dbContext.SensorNodes.AsNoTracking().OrderBy(s => s.Name);
        var total = await query.CountAsync(ct);
        var items = await query.Skip(offset).Take(limit).ToListAsync(ct);
        var columns = new List<string> { "Id", "AreaId", "Name", "Type", "IsActive", "Latitude", "Longitude" };
        var rows = items.Select(s => (IReadOnlyDictionary<string, string?>)new Dictionary<string, string?>
        {
            ["Id"] = s.Id.ToString(),
            ["AreaId"] = s.AreaId.ToString(),
            ["Name"] = s.Name,
            ["Type"] = s.Type.ToString(),
            ["IsActive"] = s.IsActive.ToString(),
            ["Latitude"] = s.Latitude.ToString("F6"),
            ["Longitude"] = s.Longitude.ToString("F6"),
        }).ToList();
        return (columns, rows, total);
    }

    private static async Task<(List<string> Columns, List<IReadOnlyDictionary<string, string?>> Rows, int TotalCount)> QueryAlertsAsync(
        NatureProtectorControlDbContext dbContext, int limit, int offset, CancellationToken ct)
    {
        var query = dbContext.AlertStates.AsNoTracking().OrderByDescending(a => a.TriggeredAt);
        var total = await query.CountAsync(ct);
        var items = await query.Skip(offset).Take(limit).ToListAsync(ct);
        var columns = new List<string> { "Id", "AreaId", "AlertCode", "Severity", "Status", "TriggeredAt", "ResolvedAt" };
        var rows = items.Select(a => (IReadOnlyDictionary<string, string?>)new Dictionary<string, string?>
        {
            ["Id"] = a.Id.ToString(),
            ["AreaId"] = a.AreaId.ToString(),
            ["AlertCode"] = a.AlertCode,
            ["Severity"] = a.Severity,
            ["Status"] = a.Status,
            ["TriggeredAt"] = a.TriggeredAt.ToString("o"),
            ["ResolvedAt"] = a.ResolvedAt?.ToString("o"),
        }).ToList();
        return (columns, rows, total);
    }

    private static async Task<(List<string> Columns, List<IReadOnlyDictionary<string, string?>> Rows, int TotalCount)> QueryGridCellsAsync(
        NatureProtectorControlDbContext dbContext, int limit, int offset, CancellationToken ct)
    {
        var query = dbContext.GridCells.AsNoTracking().OrderBy(g => g.CellCode);
        var total = await query.CountAsync(ct);
        var items = await query.Skip(offset).Take(limit).ToListAsync(ct);
        var columns = new List<string> { "Id", "AreaId", "CellCode", "CentroidLatitude", "CentroidLongitude" };
        var rows = items.Select(g => (IReadOnlyDictionary<string, string?>)new Dictionary<string, string?>
        {
            ["Id"] = g.Id.ToString(),
            ["AreaId"] = g.AreaId.ToString(),
            ["CellCode"] = g.CellCode,
            ["CentroidLatitude"] = g.CentroidLatitude.ToString("F6"),
            ["CentroidLongitude"] = g.CentroidLongitude.ToString("F6"),
        }).ToList();
        return (columns, rows, total);
    }
}
