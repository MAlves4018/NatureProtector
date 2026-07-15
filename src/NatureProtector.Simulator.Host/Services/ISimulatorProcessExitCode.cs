namespace NatureProtector.Simulator.Host.Services;

public interface ISimulatorProcessExitCode
{
    void MarkFailure();
}

public sealed class EnvironmentSimulatorProcessExitCode : ISimulatorProcessExitCode
{
    public void MarkFailure()
    {
        Environment.ExitCode = 1;
    }
}
