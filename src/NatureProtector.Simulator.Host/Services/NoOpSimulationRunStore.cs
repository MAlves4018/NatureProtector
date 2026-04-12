using NatureProtector.Core.Scenarios;

namespace NatureProtector.Simulator.Host.Services;

public sealed class NoOpSimulationRunStore : ISimulationRunStore
{
    public Task UpsertAsync(SimulationContext context, SimulationRun run, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(context);
        ArgumentNullException.ThrowIfNull(run);
        return Task.CompletedTask;
    }
}
