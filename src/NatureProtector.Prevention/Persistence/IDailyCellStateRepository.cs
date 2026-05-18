using NatureProtector.Prevention.Readings;
using NatureProtector.Prevention.Risk;

namespace NatureProtector.Prevention.Persistence;

public sealed record DailyCellStateLookupResult(
    DailyCellState? State,
    Guid? GridCellId,
    Guid? ConfigurationVersionId);

public interface IDailyCellStateRepository
{
    Task<DailyCellStateLookupResult> GetForReadingAsync(
        NormalizedReading reading,
        Guid? simulationRunId,
        CancellationToken cancellationToken);

    Task UpsertAsync(
        RiskInput input,
        CancellationToken cancellationToken);
}
