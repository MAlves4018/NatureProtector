using System.Reflection;
using NatureProtector.Simulator.Host.Services;

namespace NatureProtector.Simulator.Host.Tests.Helpers;

internal static class SimulationRunnerInvoker
{
    public static Task ExecuteAsync(SimulationRunner runner, CancellationToken cancellationToken)
    {
        var method = typeof(SimulationRunner).GetMethod(
            "ExecuteAsync",
            BindingFlags.Instance | BindingFlags.NonPublic);

        Assert.NotNull(method);

        var task = method!.Invoke(runner, [cancellationToken]) as Task;
        Assert.NotNull(task);

        return task!;
    }
}
