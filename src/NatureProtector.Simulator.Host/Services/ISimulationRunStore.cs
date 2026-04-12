using NatureProtector.Core.Scenarios;

namespace NatureProtector.Simulator.Host.Services;

public interface ISimulationRunStore
{
    Task UpsertAsync(SimulationContext context, SimulationRun run, CancellationToken cancellationToken);
}
