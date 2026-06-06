namespace NatureProtector.Simulator.Host.ControlledValidation;

public static class ControlledValidationEnvironmentGuard
{
    public static bool IsAllowed(string? environmentName)
    {
        return string.Equals(environmentName, "Development", StringComparison.OrdinalIgnoreCase) ||
               string.Equals(environmentName, "Evidence", StringComparison.OrdinalIgnoreCase);
    }

    public static void EnsureAllowed(string? environmentName)
    {
        if (IsAllowed(environmentName))
        {
            return;
        }

        throw new InvalidOperationException(
            "Controlled validation P0 can only run in Development or Evidence environments.");
    }
}
