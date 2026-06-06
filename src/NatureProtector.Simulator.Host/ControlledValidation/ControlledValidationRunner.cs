namespace NatureProtector.Simulator.Host.ControlledValidation;

public sealed class ControlledValidationRunner(
    ILogger<ControlledValidationRunner> logger,
    ControlledValidationOrchestrator orchestrator,
    IHostApplicationLifetime applicationLifetime) : BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        try
        {
            await orchestrator.PublishAsync(stoppingToken);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Controlled validation publication failed.");
            throw;
        }
        finally
        {
            applicationLifetime.StopApplication();
        }
    }
}
