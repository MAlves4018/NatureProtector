using NatureProtector.IntegrationTests.TestInfrastructure;
using System.Net;

namespace NatureProtector.IntegrationTests.Flow;

public sealed partial class DockerPublishedRuntimeProcessTests
{
    private const string OperationalAuditEnvironmentVariable =
        "NP_RUN_OPERATIONAL_AUDIT_PHASE1";
    private const string BackofficeReadinessProofEnvironmentVariable =
        "NP_RUN_BACKOFFICE_READINESS_PHASE3C";
    private const string PreventionReadinessProofEnvironmentVariable =
        "NP_RUN_PREVENTION_READINESS_PHASE3D";

    [Fact]
    [Trait("Category", "DockerIntegration")]
    [Trait("Purpose", "OperationalAudit")]
    public async Task BackofficeReadiness_BecomesUnhealthyAfterItsPostgresDatabaseIsDropped()
    {
        if (!IsBackofficeReadinessProofEnabled())
        {
            return;
        }

        await using var database = await TemporaryPostgresDatabase.CreateAsync();
        var repositoryRoot = ResolveRepositoryRoot();
        var runRoot = Path.Combine(
            repositoryRoot,
            "artifacts",
            "tests",
            "phase3c-backoffice-readiness-postgres-down",
            Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(runRoot);

        var apiPublishDirectory = Path.Combine(runRoot, "api");
        await PublishProjectAsync(
            repositoryRoot,
            "src/NatureProtector.Backoffice.Api/NatureProtector.Backoffice.Api.csproj",
            apiPublishDirectory,
            Path.Combine(runRoot, "publish-api"));

        var postgresSettings = DockerIntegrationSettings.CreatePostgresSettings(
            database.DatabaseName);
        var rabbitMqOptions = DockerIntegrationSettings.CreateRabbitMqOptions(
            $"np.audit.api-health.{Guid.NewGuid():N}");
        var apiPort = GetFreeTcpPort();
        var environment = WithAspNetCoreUrl(
            CreateRuntimeEnvironment(postgresSettings, rabbitMqOptions, Guid.NewGuid()),
            apiPort);

        StartedProcess? apiProcess = null;

        try
        {
            apiProcess = StartPublishedProcess(
                "Backoffice API phase-3C readiness remediation proof",
                apiPublishDirectory,
                "NatureProtector.Backoffice.Api.dll",
                environment,
                Path.Combine(runRoot, "api"));

            var apiBaseUrl = $"http://127.0.0.1:{apiPort}";
            await WaitForHttpStatusAsync(
                apiBaseUrl + "/health/ready",
                HttpStatusCode.OK,
                apiProcess,
                TimeSpan.FromSeconds(60));

            await database.DropAsync();

            await WaitForHttpStatusAsync(
                apiBaseUrl + "/api/control/areas",
                HttpStatusCode.ServiceUnavailable,
                apiProcess,
                TimeSpan.FromSeconds(30));

            await WaitForHttpStatusAsync(
                apiBaseUrl + "/health/live",
                HttpStatusCode.OK,
                apiProcess,
                TimeSpan.FromSeconds(10));
            await WaitForHttpStatusAsync(
                apiBaseUrl + "/health/ready",
                HttpStatusCode.ServiceUnavailable,
                apiProcess,
                TimeSpan.FromSeconds(15));
            await WaitForHttpStatusAsync(
                apiBaseUrl + "/health",
                HttpStatusCode.ServiceUnavailable,
                apiProcess,
                TimeSpan.FromSeconds(15));

            Assert.False(
                apiProcess.HasExited,
                "Backoffice exited instead of remaining live while PostgreSQL was unavailable.");

            Console.WriteLine(
                "PHASE3C_BACKOFFICE_READINESS_REMEDIATED " +
                "postgres_available=false control_endpoint=503 live=200 ready=503 aggregate=503");
        }
        finally
        {
            if (apiProcess is not null)
            {
                await apiProcess.StopAsync();
            }
        }

        AssertProcessStopped(apiProcess);
    }

    [Fact]
    [Trait("Category", "DockerIntegration")]
    [Trait("Purpose", "OperationalAudit")]
    public async Task PreventionReadiness_BecomesUnhealthyAfterPostgresDrops_AndRecoversAfterRecreation()
    {
        if (!IsPreventionReadinessProofEnabled())
        {
            return;
        }

        await using var database = await TemporaryPostgresDatabase.CreateAsync();
        var exchangeName = $"np.audit.prevention-health.{Guid.NewGuid():N}";
        var baseRabbitMqOptions = DockerIntegrationSettings.CreateRabbitMqOptions(exchangeName);
        await using var virtualHost = await TemporaryRabbitMqVirtualHost.CreateAsync(
            baseRabbitMqOptions,
            CancellationToken.None);
        var rabbitMqOptions = virtualHost.CreateOptions(exchangeName);

        var repositoryRoot = ResolveRepositoryRoot();
        var runRoot = Path.Combine(
            repositoryRoot,
            "artifacts",
            "tests",
            "phase3d-prevention-readiness-postgres-down",
            Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(runRoot);

        var preventionPublishDirectory = Path.Combine(runRoot, "prevention");
        await PublishProjectAsync(
            repositoryRoot,
            "src/NatureProtector.Prevention.Host/NatureProtector.Prevention.Host.csproj",
            preventionPublishDirectory,
            Path.Combine(runRoot, "publish-prevention"));

        var postgresSettings = DockerIntegrationSettings.CreatePostgresSettings(
            database.DatabaseName);
        var preventionPort = GetFreeTcpPort();
        var environment = WithAspNetCoreUrl(
            CreateRuntimeEnvironment(postgresSettings, rabbitMqOptions, Guid.NewGuid()),
            preventionPort);

        StartedProcess? preventionProcess = null;

        try
        {
            preventionProcess = StartPublishedProcess(
                "Prevention Host phase-3D readiness remediation proof",
                preventionPublishDirectory,
                "NatureProtector.Prevention.Host.dll",
                environment,
                Path.Combine(runRoot, "prevention"));

            var preventionBaseUrl = $"http://127.0.0.1:{preventionPort}";
            await WaitForHttpStatusAsync(
                preventionBaseUrl + "/health/ready",
                HttpStatusCode.OK,
                preventionProcess,
                TimeSpan.FromSeconds(60));
            await WaitForConsumerAsync(
                virtualHost.CreateConnectionFactory(),
                rabbitMqOptions.IngestionReadingsQueueName);

            await database.DropAsync();

            await WaitForHttpStatusAsync(
                preventionBaseUrl + "/health/live",
                HttpStatusCode.OK,
                preventionProcess,
                TimeSpan.FromSeconds(10));
            await WaitForHttpStatusAsync(
                preventionBaseUrl + "/health/ready",
                HttpStatusCode.ServiceUnavailable,
                preventionProcess,
                TimeSpan.FromSeconds(20));

            Assert.False(
                preventionProcess.HasExited,
                "Prevention exited instead of remaining live while PostgreSQL was unavailable.");

            await database.RecreateAsync();

            await WaitForHttpStatusAsync(
                preventionBaseUrl + "/health/ready",
                HttpStatusCode.OK,
                preventionProcess,
                TimeSpan.FromSeconds(30));
            await WaitForConsumerAsync(
                virtualHost.CreateConnectionFactory(),
                rabbitMqOptions.IngestionReadingsQueueName);

            Assert.False(
                preventionProcess.HasExited,
                "Prevention exited instead of recovering readiness after PostgreSQL returned.");

            Console.WriteLine(
                "PHASE3D_PREVENTION_READINESS_REMEDIATED " +
                "persistence_enabled=true rabbitmq_consumer=true " +
                "postgres_transition=available-unavailable-available " +
                "live_during_outage=200 ready_during_outage=503 ready_after_recovery=200");
        }
        finally
        {
            if (preventionProcess is not null)
            {
                await preventionProcess.StopAsync();
            }
        }

        AssertProcessStopped(preventionProcess);
    }

    [Fact]
    [Trait("Category", "DockerIntegration")]
    [Trait("Purpose", "OperationalAudit")]
    public async Task PreventionReadiness_DoesNotRequirePostgres_WhenPersistenceIsDisabled()
    {
        if (!IsPreventionReadinessProofEnabled())
        {
            return;
        }

        var exchangeName = $"np.audit.prevention-in-memory-health.{Guid.NewGuid():N}";
        var baseRabbitMqOptions = DockerIntegrationSettings.CreateRabbitMqOptions(exchangeName);
        await using var virtualHost = await TemporaryRabbitMqVirtualHost.CreateAsync(
            baseRabbitMqOptions,
            CancellationToken.None);
        var rabbitMqOptions = virtualHost.CreateOptions(exchangeName);

        var repositoryRoot = ResolveRepositoryRoot();
        var runRoot = Path.Combine(
            repositoryRoot,
            "artifacts",
            "tests",
            "phase3d-prevention-readiness-in-memory",
            Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(runRoot);

        var preventionPublishDirectory = Path.Combine(runRoot, "prevention");
        await PublishProjectAsync(
            repositoryRoot,
            "src/NatureProtector.Prevention.Host/NatureProtector.Prevention.Host.csproj",
            preventionPublishDirectory,
            Path.Combine(runRoot, "publish-prevention"));

        var unusedPostgresSettings = DockerIntegrationSettings.CreatePostgresSettings(
            $"np_absent_{Guid.NewGuid():N}");
        var preventionPort = GetFreeTcpPort();
        var environment = new Dictionary<string, string>(
            WithAspNetCoreUrl(
                CreateRuntimeEnvironment(unusedPostgresSettings, rabbitMqOptions, Guid.NewGuid()),
                preventionPort),
            StringComparer.Ordinal)
        {
            ["PreventionHost__PipelinePersistenceEnabled"] = "false",
            ["POSTGRES_HOST"] = "127.0.0.1",
            ["POSTGRES_PORT"] = "1"
        };

        StartedProcess? preventionProcess = null;

        try
        {
            preventionProcess = StartPublishedProcess(
                "Prevention Host phase-3D in-memory readiness proof",
                preventionPublishDirectory,
                "NatureProtector.Prevention.Host.dll",
                environment,
                Path.Combine(runRoot, "prevention"));

            var preventionBaseUrl = $"http://127.0.0.1:{preventionPort}";
            await WaitForHttpStatusAsync(
                preventionBaseUrl + "/health/live",
                HttpStatusCode.OK,
                preventionProcess,
                TimeSpan.FromSeconds(30));
            await WaitForHttpStatusAsync(
                preventionBaseUrl + "/health/ready",
                HttpStatusCode.OK,
                preventionProcess,
                TimeSpan.FromSeconds(60));
            await WaitForConsumerAsync(
                virtualHost.CreateConnectionFactory(),
                rabbitMqOptions.IngestionReadingsQueueName);

            Assert.False(
                preventionProcess.HasExited,
                "Prevention in-memory mode unexpectedly required PostgreSQL readiness.");

            Console.WriteLine(
                "PHASE3D_PREVENTION_IN_MEMORY_READINESS_PROVED " +
                "persistence_enabled=false postgres_config_unreachable=true " +
                "rabbitmq_consumer=true live=200 ready=200");
        }
        finally
        {
            if (preventionProcess is not null)
            {
                await preventionProcess.StopAsync();
            }
        }

        AssertProcessStopped(preventionProcess);
    }

    private static bool IsBackofficeReadinessProofEnabled()
    {
        var enabled = string.Equals(
                Environment.GetEnvironmentVariable(BackofficeReadinessProofEnvironmentVariable),
                "true",
                StringComparison.OrdinalIgnoreCase)
            || string.Equals(
                Environment.GetEnvironmentVariable(OperationalAuditEnvironmentVariable),
                "true",
                StringComparison.OrdinalIgnoreCase);

        if (!enabled)
        {
            Console.WriteLine(
                $"SKIPPED_ENV_REQUIRED: set {BackofficeReadinessProofEnvironmentVariable}=true " +
                $"or {OperationalAuditEnvironmentVariable}=true to execute the Backoffice readiness proof.");
        }

        return enabled;
    }

    private static bool IsPreventionReadinessProofEnabled()
    {
        var enabled = string.Equals(
                Environment.GetEnvironmentVariable(PreventionReadinessProofEnvironmentVariable),
                "true",
                StringComparison.OrdinalIgnoreCase)
            || string.Equals(
                Environment.GetEnvironmentVariable(OperationalAuditEnvironmentVariable),
                "true",
                StringComparison.OrdinalIgnoreCase);

        if (!enabled)
        {
            Console.WriteLine(
                $"SKIPPED_ENV_REQUIRED: set {PreventionReadinessProofEnvironmentVariable}=true " +
                $"or {OperationalAuditEnvironmentVariable}=true to execute the Prevention readiness proof.");
        }

        return enabled;
    }


}
