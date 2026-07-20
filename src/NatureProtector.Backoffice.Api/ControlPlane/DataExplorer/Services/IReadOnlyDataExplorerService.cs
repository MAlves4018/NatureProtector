using NatureProtector.Backoffice.Api.ControlPlane.DataExplorer.Contracts;

namespace NatureProtector.Backoffice.Api.ControlPlane.DataExplorer.Services;

public interface IReadOnlyDataExplorerService
{
    Task<IReadOnlyList<DatasetDefinition>> ListDatasetsAsync(CancellationToken cancellationToken);
    Task<DataExplorerQueryResponse?> QueryAsync(DataExplorerQueryRequest request, CancellationToken cancellationToken);
}
