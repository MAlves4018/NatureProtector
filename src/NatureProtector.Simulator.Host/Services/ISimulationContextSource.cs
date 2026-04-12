namespace NatureProtector.Simulator.Host.Services;

public interface ISimulationContextSource
{
    Task<SimulationContext> CreateAsync(CancellationToken cancellationToken);
}
