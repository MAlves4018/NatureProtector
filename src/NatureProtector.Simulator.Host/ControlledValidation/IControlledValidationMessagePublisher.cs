namespace NatureProtector.Simulator.Host.ControlledValidation;

public interface IControlledValidationMessagePublisher
{
    Task PublishAsync(
        ControlledValidationMessage message,
        CancellationToken cancellationToken = default);
}
