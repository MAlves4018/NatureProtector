using Microsoft.Extensions.FileProviders;
using Microsoft.Extensions.Hosting;
using NatureProtector.Backoffice.Api.Configuration;

namespace NatureProtector.Backoffice.Api.Tests;

public sealed class BackofficeApiOptionsValidatorTests
{
    [Fact]
    public void Validate_Development_AllowsLocalProcessLaunch()
    {
        var validator = new BackofficeApiOptionsValidator(Environment("Development"));
        var options = new BackofficeApiOptions
        {
            ControlPlaneEnabled = true,
            LocalRuntimeProcessLaunchEnabled = true
        };

        var result = validator.Validate(name: null, options);

        Assert.True(result.Succeeded);
    }

    [Theory]
    [InlineData("Staging")]
    [InlineData("Production")]
    public void Validate_HostedEnvironment_RejectsLocalProcessLaunch(string environmentName)
    {
        var validator = new BackofficeApiOptionsValidator(Environment(environmentName));
        var options = new BackofficeApiOptions
        {
            ControlPlaneEnabled = true,
            LocalRuntimeProcessLaunchEnabled = true
        };

        var result = validator.Validate(name: null, options);

        Assert.True(result.Failed);
        Assert.Contains(
            "BackofficeApi:LocalRuntimeProcessLaunchEnabled cannot be true outside Development. Use an environment-specific distributed orchestrator or keep local process launch disabled.",
            result.Failures);
    }

    [Fact]
    public void Validate_Production_AllowsControlPlaneWithLocalLaunchDisabled()
    {
        var validator = new BackofficeApiOptionsValidator(Environment("Production"));
        var options = new BackofficeApiOptions
        {
            ControlPlaneEnabled = true,
            LocalRuntimeProcessLaunchEnabled = false
        };

        var result = validator.Validate(name: null, options);

        Assert.True(result.Succeeded);
    }

    private static IHostEnvironment Environment(string name) => new TestHostEnvironment
    {
        EnvironmentName = name
    };

    private sealed class TestHostEnvironment : IHostEnvironment
    {
        public string EnvironmentName { get; set; } = Environments.Production;
        public string ApplicationName { get; set; } = "NatureProtector.Backoffice.Api.Tests";
        public string ContentRootPath { get; set; } = AppContext.BaseDirectory;
        public IFileProvider ContentRootFileProvider { get; set; } = new NullFileProvider();
    }
}
