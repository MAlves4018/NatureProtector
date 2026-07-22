namespace NatureProtector.Backoffice.Api.ControlPlane.DataExplorer.Contracts;

public sealed record DatasetDefinition(
    string DatasetId,
    string DisplayName,
    string Description,
    IReadOnlyList<string> Columns,
    int MaxLimit,
    int MaxOffset);

public sealed record DataExplorerQueryRequest(
    string DatasetId,
    int Limit = 100,
    int Offset = 0);

public sealed record DataExplorerQueryResponse(
    string DatasetId,
    string DisplayName,
    IReadOnlyList<string> Columns,
    IReadOnlyList<IReadOnlyDictionary<string, string?>> Rows,
    int TotalCount,
    int Limit,
    int Offset,
    IReadOnlyList<string> Limitations);
