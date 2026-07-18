namespace NatureProtector.Backoffice.Api.Tests;

public sealed class RabbitMqManagementRegistrationTests
{
    [Fact]
    public void Host_registers_dedicated_validated_management_client_only_with_active_control_plane()
    {
        var root = FindRepositoryRoot();
        var program = File.ReadAllText(
            Path.Combine(root, "src", "NatureProtector.Backoffice.Api", "Program.cs"));

        Assert.Contains("if (backofficeOptions.ControlPlaneEnabled)", program);
        Assert.Contains("AddRabbitMqManagementHttpClient(builder.Configuration)", program);
    }

    [Fact]
    public void Runtime_observability_uses_dedicated_client_for_rabbitmq_and_default_client_for_other_http_health()
    {
        var root = FindRepositoryRoot();
        var service = File.ReadAllText(Path.Combine(
            root,
            "src",
            "NatureProtector.Backoffice.Api",
            "ControlPlane",
            "Services",
            "RuntimeObservabilityService.cs"));

        Assert.Contains("CreateClient(RabbitMqManagementHttpClient.ClientName)", service);
        Assert.Contains("RabbitMqManagementHttpClient.BuildQueuesUri(options)", service);
        Assert.Contains("GetEffectiveManagementUserName()", service);
        Assert.Contains("GetEffectiveManagementPassword()", service);
        Assert.Contains("var client = _httpClientFactory.CreateClient();", service);
        Assert.DoesNotContain("new Uri($\"http://{hostName}", service);
    }

    private static string FindRepositoryRoot()
    {
        var current = new DirectoryInfo(AppContext.BaseDirectory);
        while (current is not null)
        {
            if (File.Exists(Path.Combine(current.FullName, "NatureProtector.sln")))
            {
                return current.FullName;
            }

            current = current.Parent;
        }

        throw new InvalidOperationException("Repository root could not be found.");
    }
}
