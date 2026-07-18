using NatureProtector.Simulator.Host.Publishing;
using NatureProtector.Simulator.Host.Services;

namespace NatureProtector.Simulator.Host.ControlledValidation;

public sealed class ControlledValidationRunner(
    ILogger<ControlledValidationRunner> logger,
    ControlledValidationOrchestrator orchestrator,
    ISimulatorProcessExitCode processExitCode,
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
            processExitCode.MarkFailure();
            logger.LogError(
                ex,
                "Controlled validation publication failed | FailureType={FailureType} | " +
                "PossiblePartialDelivery={PossiblePartialDelivery}",
                ex.GetType().FullName,
                ex is RabbitMqPublishException publishException &&
                publishException.PossiblePartialDelivery);
            throw;
        }
        finally
        {
            applicationLifetime.StopApplication();
        }
    }
}
