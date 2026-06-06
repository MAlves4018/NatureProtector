using NatureProtector.Simulator.Host.ControlledValidation;

namespace NatureProtector.Simulator.Host.Tests.ControlledValidation;

public sealed class ControlledValidationEnvironmentGuardTests
{
    [Theory]
    [InlineData("Development")]
    [InlineData("Evidence")]
    [InlineData("evidence")]
    public void EnsureAllowed_AllowsDevelopmentAndEvidence(string environmentName)
    {
        ControlledValidationEnvironmentGuard.EnsureAllowed(environmentName);
    }

    [Theory]
    [InlineData("Production")]
    [InlineData("Staging")]
    [InlineData("")]
    public void EnsureAllowed_ThrowsOutsideAllowedEnvironments(string environmentName)
    {
        var ex = Assert.Throws<InvalidOperationException>(() =>
            ControlledValidationEnvironmentGuard.EnsureAllowed(environmentName));

        Assert.Equal(
            "Controlled validation P0 can only run in Development or Evidence environments.",
            ex.Message);
    }
}
