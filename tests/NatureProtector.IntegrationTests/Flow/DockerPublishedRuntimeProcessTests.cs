using Microsoft.EntityFrameworkCore;
using NatureProtector.Core.Scenarios;
using NatureProtector.Core.Sensors;
using NatureProtector.Infrastructure.Postgres.Control;
using NatureProtector.Infrastructure.Postgres.Pipeline;
using NatureProtector.IntegrationTests.TestInfrastructure;
using RabbitMQ.Client;
using System.Diagnostics;
using System.Net;
using System.Net.Sockets;

namespace NatureProtector.IntegrationTests.Flow;

[Collection(DockerIntegrationCollection.Name)]
public sealed class DockerPublishedRuntimeProcessTests
{
    private const string AreaCode = "process-smoke-area";
    private const string ScenarioCode = "process_smoke_scenario";

    [Fact]
    [Trait("Category", "DockerIntegration")]
    public async Task PublishedRuntimeProcesses_RunSimulatorPreventionApiPath_AndCleanUp()
    {
        await using var database = await TemporaryPostgresDatabase.CreateAsync();
        var exchangeName = $"np.it.process.{Guid.NewGuid():N}";
        var baseOptions = DockerIntegrationSettings.CreateRabbitMqOptions(exchangeName);
        await using var virtualHost = await TemporaryRabbitMqVirtualHost.CreateAsync(baseOptions, CancellationToken.None);
        var rabbitMqOptions = virtualHost.CreateOptions(exchangeName);
        var repositoryRoot = ResolveRepositoryRoot();
        var runRoot = Path.Combine(
            repositoryRoot,
            "artifacts",
            "tests",
            "process-smoke",
            Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(runRoot);

        var seeded = await SeedProcessSmokeControlPlaneAsync(database);
        var postgresSettings = DockerIntegrationSettings.CreatePostgresSettings(database.DatabaseName);
        var apiPort = GetFreeTcpPort();
        var preventionPort = GetFreeTcpPort();
        var environment = CreateRuntimeEnvironment(
            postgresSettings,
            rabbitMqOptions,
            seeded.AreaId);

        var apiPublishDir = Path.Combine(runRoot, "api");
        var preventionPublishDir = Path.Combine(runRoot, "prevention");
        var simulatorPublishDir = Path.Combine(runRoot, "simulator");

        await PublishProjectAsync(
            repositoryRoot,
            "src/NatureProtector.Backoffice.Api/NatureProtector.Backoffice.Api.csproj",
            apiPublishDir,
            Path.Combine(runRoot, "publish-api"));
        await PublishProjectAsync(
            repositoryRoot,
            "src/NatureProtector.Prevention.Host/NatureProtector.Prevention.Host.csproj",
            preventionPublishDir,
            Path.Combine(runRoot, "publish-prevention"));
        await PublishProjectAsync(
            repositoryRoot,
            "src/NatureProtector.Simulator.Host/NatureProtector.Simulator.Host.csproj",
            simulatorPublishDir,
            Path.Combine(runRoot, "publish-simulator"));

        StartedProcess? apiProcess = null;
        StartedProcess? preventionProcess = null;
        StartedProcess? simulatorProcess = null;

        try
        {
            apiProcess = StartPublishedProcess(
                "Backoffice API",
                apiPublishDir,
                "NatureProtector.Backoffice.Api.dll",
                WithAspNetCoreUrl(environment, apiPort),
                Path.Combine(runRoot, "api"));
            var apiBaseUrl = $"http://127.0.0.1:{apiPort}";
            await WaitForHttpSuccessAsync(apiBaseUrl + "/health", apiProcess, TimeSpan.FromSeconds(60));
            var areasJson = await WaitForHttpSuccessAsync(
                apiBaseUrl + "/api/control/areas",
                apiProcess,
                TimeSpan.FromSeconds(60));
            Assert.Contains(AreaCode, areasJson, StringComparison.Ordinal);

            preventionProcess = StartPublishedProcess(
                "Prevention Host",
                preventionPublishDir,
                "NatureProtector.Prevention.Host.dll",
                WithAspNetCoreUrl(environment, preventionPort),
                Path.Combine(runRoot, "prevention"));
            await WaitForConsumerAsync(
                virtualHost.CreateConnectionFactory(),
                rabbitMqOptions.IngestionReadingsQueueName);

            simulatorProcess = StartPublishedProcess(
                "Simulator Host",
                simulatorPublishDir,
                "NatureProtector.Simulator.Host.dll",
                environment,
                Path.Combine(runRoot, "simulator"));
            var simulatorExitCode = await simulatorProcess.WaitForExitAndCaptureAsync(TimeSpan.FromSeconds(90));
            Assert.Equal(0, simulatorExitCode);

            await WaitForProcessPipelineAsync(database, seeded.AreaId, seeded.GridCellId);
            AssertNoResidualIngestionMessage(
                virtualHost.CreateConnectionFactory(),
                rabbitMqOptions.IngestionReadingsQueueName);

            var alertsJson = await WaitForHttpSuccessAsync(
                apiBaseUrl + $"/api/control/areas/{AreaCode}/alerts/active",
                apiProcess,
                TimeSpan.FromSeconds(30));
            Assert.StartsWith("[", alertsJson.Trim(), StringComparison.Ordinal);
        }
        finally
        {
            if (simulatorProcess is not null)
            {
                await simulatorProcess.StopAsync();
            }

            if (preventionProcess is not null)
            {
                await preventionProcess.StopAsync();
            }

            if (apiProcess is not null)
            {
                await apiProcess.StopAsync();
            }
        }

        AssertProcessStopped(simulatorProcess);
        AssertProcessStopped(preventionProcess);
        AssertProcessStopped(apiProcess);
    }

    [Fact]
    [Trait("Category", "DockerIntegration")]
    public async Task PublishedPreventionHost_RemainsLiveAndBecomesReadyWhenRabbitMqStartsLate()
    {
        var rabbitMqContainer =
            TryGetRabbitMqContainerName();

        if (rabbitMqContainer is null)
        {
            Console.WriteLine(
                "SKIPPED_ENV_REQUIRED: NP_TEST_RABBITMQ_CONTAINER must " +
                "identify the Docker RabbitMQ container for the delayed-start integration test.");

            return;
        }

        await using var database =
            await TemporaryPostgresDatabase.CreateAsync();

        var exchangeName =
            $"np.it.prevention.late-rabbitmq.{Guid.NewGuid():N}";

        var baseRabbitMqOptions =
            DockerIntegrationSettings.CreateRabbitMqOptions(exchangeName);

        await using var virtualHost =
            await TemporaryRabbitMqVirtualHost.CreateAsync(
                baseRabbitMqOptions,
                CancellationToken.None);

        var rabbitMqOptions =
            virtualHost.CreateOptions(exchangeName);

        var rabbitMqConnectionFactory =
            virtualHost.CreateConnectionFactory();

        rabbitMqConnectionFactory.RequestedConnectionTimeout =
            TimeSpan.FromSeconds(2);

        var repositoryRoot =
            ResolveRepositoryRoot();

        var runRoot = Path.Combine(
            repositoryRoot,
            "artifacts",
            "tests",
            "prevention-late-rabbitmq",
            Guid.NewGuid().ToString("N"));

        Directory.CreateDirectory(runRoot);

        var preventionPublishDirectory =
            Path.Combine(runRoot, "prevention");

        await PublishProjectAsync(
            repositoryRoot,
            "src/NatureProtector.Prevention.Host/NatureProtector.Prevention.Host.csproj",
            preventionPublishDirectory,
            Path.Combine(runRoot, "publish-prevention"));

        var postgresSettings =
            DockerIntegrationSettings.CreatePostgresSettings(
                database.DatabaseName);

        var preventionPort =
            GetFreeTcpPort();

        var environment =
            WithAspNetCoreUrl(
                CreateRuntimeEnvironment(
                    postgresSettings,
                    rabbitMqOptions,
                    Guid.NewGuid()),
                preventionPort);

        StartedProcess? preventionProcess = null;

        try
        {
            await RunRequiredDockerCommandAsync(
                "stop",
                "--time",
                "10",
                rabbitMqContainer);

            preventionProcess = StartPublishedProcess(
                "Prevention Host with delayed RabbitMQ",
                preventionPublishDirectory,
                "NatureProtector.Prevention.Host.dll",
                environment,
                Path.Combine(runRoot, "prevention"));

            var originalProcessId =
                preventionProcess.Id;

            var preventionBaseUrl =
                $"http://127.0.0.1:{preventionPort}";

            /*
            * O processo tem de abrir o servidor HTTP mesmo quando o RabbitMQ
            * ainda não está disponível.
            */
            await WaitForHttpStatusAsync(
                preventionBaseUrl + "/health/live",
                HttpStatusCode.OK,
                preventionProcess,
                TimeSpan.FromSeconds(30));

            /*
            * Vivo, mas ainda não operacional: o consumidor não está ligado
            * ao broker, portanto readiness deve indicar 503.
            */
            await WaitForHttpStatusAsync(
                preventionBaseUrl + "/health/ready",
                HttpStatusCode.ServiceUnavailable,
                preventionProcess,
                TimeSpan.FromSeconds(30));

            Assert.False(
                preventionProcess.HasExited,
                "The Prevention process terminated while RabbitMQ was unavailable.");

            Assert.Equal(
                originalProcessId,
                preventionProcess.Id);

            /*
            * O RabbitMQ começa depois do Prevention, tal como aconteceu no
            * rollout do GKE.
            */
            await RunRequiredDockerCommandAsync(
                "start",
                rabbitMqContainer);

            await WaitForRabbitMqConnectableAsync(
                rabbitMqConnectionFactory,
                TimeSpan.FromSeconds(60));

            /*
            * O mesmo processo deve recuperar sozinho, criar o consumidor
            * e passar a Ready.
            */
            await WaitForHttpStatusAsync(
                preventionBaseUrl + "/health/ready",
                HttpStatusCode.OK,
                preventionProcess,
                TimeSpan.FromSeconds(90));

            await WaitForConsumerAsync(
                rabbitMqConnectionFactory,
                rabbitMqOptions.IngestionReadingsQueueName);

            Assert.False(
                preventionProcess.HasExited,
                "The Prevention process exited instead of recovering after RabbitMQ started.");

            Assert.Equal(
                originalProcessId,
                preventionProcess.Id);

            /*
            * Evita um falso positivo em que readiness sobe e o processo
            * termina imediatamente depois.
            */
            await Task.Delay(TimeSpan.FromSeconds(2));

            Assert.False(
                preventionProcess.HasExited,
                "The Prevention process became ready but terminated immediately afterwards.");
        }
        finally
        {
            try
            {
                if (preventionProcess is not null)
                {
                    await preventionProcess.StopAsync();
                }
            }
            finally
            {
                /*
                * Garante que os restantes testes recebem novamente o
                * RabbitMQ ativo, mesmo quando este teste falha.
                */
                await EnsureDockerContainerRunningAsync(
                    rabbitMqContainer);

                await WaitForRabbitMqConnectableAsync(
                    rabbitMqConnectionFactory,
                    TimeSpan.FromSeconds(60));
            }
        }

        AssertProcessStopped(preventionProcess);

        Assert.NotNull(preventionProcess);

        var combinedLogs =
            (await File.ReadAllTextAsync(
                preventionProcess.StandardOutputPath)) +
            Environment.NewLine +
            (await File.ReadAllTextAsync(
                preventionProcess.StandardErrorPath));

        Assert.DoesNotContain(
            "Hosting failed to start",
            combinedLogs,
            StringComparison.OrdinalIgnoreCase);

        Assert.DoesNotContain(
            "Unhandled exception",
            combinedLogs,
            StringComparison.OrdinalIgnoreCase);
    }

    private static async Task PublishProjectAsync(
        string repositoryRoot,
        string relativeProjectPath,
        string outputDirectory,
        string logPrefix)
    {
        Directory.CreateDirectory(outputDirectory);
        using var process = StartedProcess.Start(
            $"publish {relativeProjectPath}",
            "dotnet",
            [
                "publish",
                relativeProjectPath,
                "-c",
                "Release",
                "--no-build",
                "--no-restore",
                "-o",
                outputDirectory,
                "/nodeReuse:false"
            ],
            repositoryRoot,
            environment: new Dictionary<string, string>(),
            logPrefix);

        var exitCode = await process.WaitForExitAndCaptureAsync(TimeSpan.FromMinutes(3));
        Assert.Equal(0, exitCode);
    }

    private static StartedProcess StartPublishedProcess(
        string name,
        string publishDirectory,
        string assemblyName,
        IReadOnlyDictionary<string, string> environment,
        string logPrefix)
    {
        return StartedProcess.Start(
            name,
            "dotnet",
            [Path.Combine(publishDirectory, assemblyName)],
            publishDirectory,
            environment,
            logPrefix);
    }

    private static IReadOnlyDictionary<string, string> CreateRuntimeEnvironment(
        NatureProtector.Infrastructure.Postgres.Configuration.PostgresControlPlaneConnectionSettings postgres,
        NatureProtector.Shared.Configuration.RabbitMqOptions rabbitMq,
        Guid areaId)
    {
        return new Dictionary<string, string>(StringComparer.Ordinal)
        {
            ["DOTNET_ENVIRONMENT"] = "Development",
            ["ASPNETCORE_ENVIRONMENT"] = "Development",
            ["POSTGRES_HOST"] = postgres.Host,
            ["POSTGRES_PORT"] = postgres.Port.ToString(),
            ["POSTGRES_DB"] = postgres.Database,
            ["POSTGRES_USER"] = postgres.Username,
            ["POSTGRES_PASSWORD"] = postgres.Password,
            ["RabbitMq__HostName"] = rabbitMq.HostName,
            ["RabbitMq__Port"] = rabbitMq.Port.ToString(),
            ["RabbitMq__UserName"] = rabbitMq.UserName,
            ["RabbitMq__Password"] = rabbitMq.Password,
            ["RabbitMq__VirtualHost"] = rabbitMq.VirtualHost,
            ["RabbitMq__ExchangeName"] = rabbitMq.ExchangeName,
            ["RabbitMq__IngestionReadingsQueueName"] = rabbitMq.IngestionReadingsQueueName,
            ["RabbitMq__ObservabilityRawQueueName"] = rabbitMq.ObservabilityRawQueueName,
            ["PreventionHost__ConsumerPrefetchCount"] = "1",
            ["PreventionHost__MaxProcessingAttempts"] = "3",
            ["PreventionHost__RetryDelaySeconds__0"] = "0",
            ["PreventionHost__RetryDelaySeconds__1"] = "0",
            ["PreventionHost__RetryPollingIntervalSeconds"] = "1",
            ["InfluxDb__Enabled"] = "false",
            ["InfluxDb__FailPipelineOnWriteError"] = "false",
            ["Simulator__ControlPlaneEnabled"] = "true",
            ["Simulator__ControlPlaneAreaCode"] = AreaCode,
            ["Simulator__ControlPlaneScenarioCode"] = ScenarioCode,
            ["Simulator__AreaId"] = areaId.ToString(),
            ["Simulator__RunOverrides__SensorCount"] = "1",
            ["Simulator__RunOverrides__NumberOfCycles"] = "1",
            ["Simulator__RunOverrides__IntervalSeconds"] = "1",
            ["Simulator__RunOverrides__Seed"] = "123",
            ["Simulator__RunOverrides__DegradationProfile"] = "none",
            ["Simulator__RunOverrides__OrchestratorCorrelationId"] = $"process-smoke-{Guid.NewGuid():N}"
        };
    }

    private static IReadOnlyDictionary<string, string> WithAspNetCoreUrl(
        IReadOnlyDictionary<string, string> environment,
        int port)
    {
        var copy = new Dictionary<string, string>(environment, StringComparer.Ordinal)
        {
            ["ASPNETCORE_URLS"] = $"http://127.0.0.1:{port}"
        };
        return copy;
    }

    private static string? TryGetRabbitMqContainerName()
    {
        var containerName =
            Environment.GetEnvironmentVariable(
                "NP_TEST_RABBITMQ_CONTAINER");

        if (string.IsNullOrWhiteSpace(containerName))
        {
            return null;
        }

        return containerName;
    }

    private static async Task WaitForHttpStatusAsync(
        string url,
        HttpStatusCode expectedStatusCode,
        StartedProcess process,
        TimeSpan timeout)
    {
        using var client = new HttpClient
        {
            Timeout = TimeSpan.FromSeconds(3)
        };

        var deadline =
            DateTimeOffset.UtcNow.Add(timeout);

        Exception? lastFailure = null;
        HttpStatusCode? lastStatusCode = null;
        string? lastResponseBody = null;

        while (DateTimeOffset.UtcNow < deadline)
        {
            if (process.HasExited)
            {
                await process.CaptureLogsAsync();

                throw new InvalidOperationException(
                    $"{process.Name} exited before '{url}' returned " +
                    $"HTTP {(int)expectedStatusCode}. " +
                    $"ExitCode={process.ExitCode}. " +
                    $"Logs: {process.StandardOutputPath} / " +
                    $"{process.StandardErrorPath}");
            }

            try
            {
                using var response =
                    await client.GetAsync(url);

                lastStatusCode =
                    response.StatusCode;

                lastResponseBody =
                    await response.Content.ReadAsStringAsync();

                if (response.StatusCode == expectedStatusCode)
                {
                    return;
                }

                lastFailure = new HttpRequestException(
                    $"Expected HTTP {(int)expectedStatusCode}, " +
                    $"but received HTTP {(int)response.StatusCode}. " +
                    $"Body: {lastResponseBody}");
            }
            catch (Exception ex)
            {
                lastFailure = ex;
            }

            await Task.Delay(
                TimeSpan.FromMilliseconds(250));
        }

        throw new TimeoutException(
            $"'{url}' did not return HTTP {(int)expectedStatusCode} " +
            $"within {timeout}. " +
            $"LastStatusCode={lastStatusCode?.ToString() ?? "<none>"}. " +
            $"LastBody={lastResponseBody ?? "<none>"}. " +
            $"LastFailure={lastFailure?.Message ?? "<none>"}");
    }

    private static async Task WaitForRabbitMqConnectableAsync(
        ConnectionFactory connectionFactory,
        TimeSpan timeout)
    {
        var deadline =
            DateTimeOffset.UtcNow.Add(timeout);

        Exception? lastFailure = null;

        while (DateTimeOffset.UtcNow < deadline)
        {
            try
            {
                using var connection =
                    connectionFactory.CreateConnection(
                        "natureprotector-late-rabbitmq-readiness");

                if (connection.IsOpen)
                {
                    return;
                }
            }
            catch (Exception ex)
            {
                lastFailure = ex;
            }

            await Task.Delay(
                TimeSpan.FromMilliseconds(500));
        }

        throw new TimeoutException(
            $"RabbitMQ did not become connectable within {timeout}.",
            lastFailure);
    }

    private static async Task EnsureDockerContainerRunningAsync(
        string containerName)
    {
        var inspectResult =
            await RunDockerCommandAsync(
                "inspect",
                "--format",
                "{{.State.Running}}",
                containerName);

        if (inspectResult.ExitCode != 0)
        {
            throw new InvalidOperationException(
                $"Could not inspect Docker container '{containerName}'. " +
                $"stdout: {inspectResult.StandardOutput.Trim()} " +
                $"stderr: {inspectResult.StandardError.Trim()}");
        }

        if (string.Equals(
            inspectResult.StandardOutput.Trim(),
            "true",
            StringComparison.OrdinalIgnoreCase))
        {
            return;
        }

        await RunRequiredDockerCommandAsync(
            "start",
            containerName);
    }

    private static async Task RunRequiredDockerCommandAsync(
        params string[] arguments)
    {
        var result =
            await RunDockerCommandAsync(arguments);

        if (result.ExitCode != 0)
        {
            throw new InvalidOperationException(
                $"Docker command failed with exit code {result.ExitCode}. " +
                $"Command: docker {string.Join(" ", arguments)}. " +
                $"stdout: {result.StandardOutput.Trim()} " +
                $"stderr: {result.StandardError.Trim()}");
        }
    }

    private static async Task<DockerCommandResult> RunDockerCommandAsync(
        params string[] arguments)
    {
        var startInfo = new ProcessStartInfo("docker")
        {
            UseShellExecute = false,
            RedirectStandardOutput = true,
            RedirectStandardError = true
        };

        foreach (var argument in arguments)
        {
            startInfo.ArgumentList.Add(argument);
        }

        using var process =
            Process.Start(startInfo)
            ?? throw new InvalidOperationException(
                "Failed to start the Docker CLI.");

        var standardOutputTask =
            process.StandardOutput.ReadToEndAsync();

        var standardErrorTask =
            process.StandardError.ReadToEndAsync();

        try
        {
            await process
                .WaitForExitAsync()
                .WaitAsync(TimeSpan.FromSeconds(60));
        }
        catch (TimeoutException)
        {
            if (!process.HasExited)
            {
                process.Kill(entireProcessTree: true);
                await process.WaitForExitAsync();
            }

            throw new TimeoutException(
                $"Docker command did not finish within 60 seconds: " +
                $"docker {string.Join(" ", arguments)}");
        }

        return new DockerCommandResult(
            process.ExitCode,
            await standardOutputTask,
            await standardErrorTask);
    }

    private sealed record DockerCommandResult(
        int ExitCode,
        string StandardOutput,
        string StandardError);

    private static async Task WaitForConsumerAsync(ConnectionFactory factory, string queueName)
    {
        Exception? lastFailure = null;

        for (var attempt = 0; attempt < 80; attempt++)
        {
            try
            {
                using var connection = factory.CreateConnection("natureprotector-process-smoke-consumer-readiness");
                using var channel = connection.CreateModel();
                var declaration = channel.QueueDeclarePassive(queueName);
                if (declaration.ConsumerCount > 0)
                {
                    return;
                }
            }
            catch (Exception ex)
            {
                lastFailure = ex;
            }

            await Task.Delay(TimeSpan.FromMilliseconds(250));
        }

        throw new TimeoutException($"RabbitMQ queue '{queueName}' did not observe a prevention process consumer.", lastFailure);
    }

    private static async Task WaitForProcessPipelineAsync(
        TemporaryPostgresDatabase database,
        Guid areaId,
        Guid gridCellId)
    {
        string? lastState = null;

        for (var attempt = 0; attempt < 120; attempt++)
        {
            await using var dbContext = database.CreateDbContext();
            var completedRuns = await dbContext.SimulationRuns.CountAsync(entity =>
                entity.AreaId == areaId &&
                entity.Status == SimulationRunStatus.Completed);
            var inboxEvents = await dbContext.InboxEvents.ToListAsync();
            var processedInbox = inboxEvents.Count(entity => entity.Status == InboxEventStatus.Processed);
            var attempts = await dbContext.ProcessingAttempts.ToListAsync();
            var acceptedReadings = await dbContext.AcceptedReadingLogs.CountAsync();
            var assessments = await dbContext.RiskAssessmentLogs.CountAsync();
            var areaSnapshots = await dbContext.AreaRiskSnapshotLogs.CountAsync(entity => entity.AreaId == areaId);
            var cellStates = await dbContext.CellOperationalStates.CountAsync(entity => entity.GridCellId == gridCellId);
            var areaStates = await dbContext.AreaOperationalStates.CountAsync(entity => entity.AreaId == areaId);

            lastState =
                $"completedRuns={completedRuns} inbox={processedInbox}/{inboxEvents.Count} " +
                $"attempts=[{string.Join(",", attempts.Select(entity => entity.Outcome))}] " +
                $"accepted={acceptedReadings} assessments={assessments} snapshots={areaSnapshots} " +
                $"cellStates={cellStates} areaStates={areaStates}";

            if (completedRuns == 1 &&
                inboxEvents.Count == 1 &&
                processedInbox == 1 &&
                attempts.Count == 1 &&
                attempts.Single().Outcome == ProcessingAttemptOutcome.Succeeded &&
                acceptedReadings == 1 &&
                assessments == 1 &&
                areaSnapshots == 1 &&
                cellStates == 1 &&
                areaStates == 1)
            {
                return;
            }

            await Task.Delay(TimeSpan.FromMilliseconds(250));
        }

        throw new TimeoutException($"Published process runtime did not reach durable processed state. Last state: {lastState}");
    }

    private static void AssertNoResidualIngestionMessage(ConnectionFactory factory, string queueName)
    {
        using var connection = factory.CreateConnection("natureprotector-process-smoke-empty-check");
        using var channel = connection.CreateModel();
        var residual = channel.BasicGet(queueName, autoAck: false);
        if (residual is not null)
        {
            channel.BasicNack(residual.DeliveryTag, multiple: false, requeue: true);
        }

        Assert.Null(residual);
    }

    private static async Task<string> WaitForHttpSuccessAsync(
        string url,
        StartedProcess process,
        TimeSpan timeout)
    {
        using var client = new HttpClient();
        var deadline = DateTimeOffset.UtcNow.Add(timeout);
        Exception? lastFailure = null;

        while (DateTimeOffset.UtcNow < deadline)
        {
            if (process.HasExited)
            {
                await process.CaptureLogsAsync();
                throw new InvalidOperationException(
                    $"{process.Name} exited before '{url}' became ready. ExitCode={process.ExitCode}. Logs: {process.StandardOutputPath} / {process.StandardErrorPath}");
            }

            try
            {
                using var response = await client.GetAsync(url);
                if (response.StatusCode == HttpStatusCode.OK)
                {
                    return await response.Content.ReadAsStringAsync();
                }

                lastFailure = new HttpRequestException($"HTTP {(int)response.StatusCode}");
            }
            catch (Exception ex)
            {
                lastFailure = ex;
            }

            await Task.Delay(TimeSpan.FromMilliseconds(250));
        }

        throw new TimeoutException($"'{url}' did not return HTTP 200 within {timeout}. Last failure: {lastFailure?.Message}");
    }

    private static async Task<SeededProcessSmokeControlPlane> SeedProcessSmokeControlPlaneAsync(
        TemporaryPostgresDatabase database)
    {
        var configurationVersionId = Guid.NewGuid();
        var areaId = Guid.NewGuid();
        var gridCellId = Guid.NewGuid();
        var profileId = Guid.NewGuid();
        var sensorId = Guid.NewGuid();
        var scenarioId = Guid.NewGuid();
        var timestamp = new DateTimeOffset(2026, 4, 6, 12, 0, 0, TimeSpan.Zero);

        await using var dbContext = database.CreateDbContext();
        dbContext.ConfigurationVersions.Add(new ConfigurationVersionRecord
        {
            Id = configurationVersionId,
            VersionNumber = 30_001,
            Description = "Published process smoke configuration",
            IsActive = true,
            CreatedAt = DateTimeOffset.UtcNow,
            CreatedBy = "integration-test"
        });
        dbContext.Areas.Add(new AreaRecord
        {
            Id = areaId,
            ConfigurationVersionId = configurationVersionId,
            Code = AreaCode,
            Name = "Published Process Smoke Area",
            CountryCode = "PT"
        });
        dbContext.GridCells.Add(new GridCellRecord
        {
            Id = gridCellId,
            AreaId = areaId,
            ConfigurationVersionId = configurationVersionId,
            CellCode = "PROCESS-SMOKE-CELL",
            CentroidLatitude = 39.8,
            CentroidLongitude = -7.9,
            LandCoverClass = "Matos",
            DominantForestType = "Florestas de pinheiro bravo",
            DominantFuelModel = "Matos",
            TreeCoverDensity = 0.55,
            StructuralHazard = "muito_alta",
            SlopeDegrees = 18.0,
            AspectDegrees = 180.0,
            AltitudeMeters = 420.0
        });
        dbContext.SensorProfiles.Add(new SensorProfileRecord
        {
            Id = profileId,
            ConfigurationVersionId = configurationVersionId,
            Name = "Published process smoke temperature profile",
            SensorFamily = "meteorological",
            PublicationPolicyJson = "{\"sampling_interval_seconds\":1,\"communication_mode\":\"RabbitMq\"}",
            NoiseProfileJson = "{\"noise_level\":0.01}",
            FaultProfileJson = "{\"latency_profile\":\"process-smoke\",\"failure_profile\":\"none\"}"
        });
        dbContext.SensorNodes.Add(new SensorNodeRecord
        {
            Id = sensorId,
            AreaId = areaId,
            GridCellId = gridCellId,
            ProfileId = profileId,
            ConfigurationVersionId = configurationVersionId,
            Name = "Process-Smoke-Temperature-Sensor",
            Type = SensorType.Temperature,
            Latitude = 39.8,
            Longitude = -7.9,
            AltitudeMeters = 420.0,
            IsActive = true,
            InstallationProfile = "process-smoke"
        });
        dbContext.ScenarioDefinitions.Add(new ScenarioDefinitionRecord
        {
            Id = scenarioId,
            AreaId = areaId,
            ConfigurationVersionId = configurationVersionId,
            Code = ScenarioCode,
            Name = "Published Process Smoke Scenario",
            ScenarioKind = ScenarioCategory.HighRisk,
            Description = "Small deterministic scenario for published process smoke testing.",
            ParametersJson = """
                {
                  "simulator_options": {
                    "BaseTemperature": 34.0,
                    "BaseHumidity": 28.0,
                    "BaseWindSpeed": 8.0,
                    "FailureRate": 0.0,
                    "NoiseLevel": 0.01,
                    "TimeAcceleration": 1.0,
                    "NumberOfCycles": 1,
                    "IntervalSeconds": 1,
                    "StartTimestamp": "2026-04-06T12:00:00Z"
                  }
                }
                """
        });

        await dbContext.SaveChangesAsync();

        return new SeededProcessSmokeControlPlane(areaId, gridCellId, sensorId, timestamp);
    }

    private static string ResolveRepositoryRoot()
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);

        while (directory is not null)
        {
            if (File.Exists(Path.Combine(directory.FullName, "NatureProtector.sln")))
            {
                return directory.FullName;
            }

            directory = directory.Parent;
        }

        throw new DirectoryNotFoundException("Could not locate NatureProtector.sln from the test output directory.");
    }

    private static int GetFreeTcpPort()
    {
        using var listener = new TcpListener(IPAddress.Loopback, 0);
        listener.Start();
        return ((IPEndPoint)listener.LocalEndpoint).Port;
    }

    private static void AssertProcessStopped(StartedProcess? process)
    {
        if (process is null)
        {
            return;
        }

        try
        {
            using var found = Process.GetProcessById(process.Id);
            Assert.True(found.HasExited, $"Process {process.Name} with PID {process.Id} is still running.");
        }
        catch (ArgumentException)
        {
            // Expected once the process has exited and Windows released the PID.
        }
    }

    private sealed record SeededProcessSmokeControlPlane(
        Guid AreaId,
        Guid GridCellId,
        Guid SensorId,
        DateTimeOffset Timestamp);

    private sealed class StartedProcess : IDisposable
    {
        private readonly Process _process;
        private readonly Task<string> _standardOutput;
        private readonly Task<string> _standardError;
        private bool _logsCaptured;

        private StartedProcess(
            string name,
            Process process,
            string standardOutputPath,
            string standardErrorPath)
        {
            Name = name;
            _process = process;
            StandardOutputPath = standardOutputPath;
            StandardErrorPath = standardErrorPath;
            _standardOutput = process.StandardOutput.ReadToEndAsync();
            _standardError = process.StandardError.ReadToEndAsync();
        }

        public string Name { get; }

        public int Id => _process.Id;

        public bool HasExited => _process.HasExited;

        public int? ExitCode => _process.HasExited ? _process.ExitCode : null;

        public string StandardOutputPath { get; }

        public string StandardErrorPath { get; }

        public static StartedProcess Start(
            string name,
            string fileName,
            IReadOnlyCollection<string> arguments,
            string workingDirectory,
            IReadOnlyDictionary<string, string> environment,
            string logPrefix)
        {
            Directory.CreateDirectory(Path.GetDirectoryName(logPrefix) ?? ".");
            var startInfo = new ProcessStartInfo(fileName)
            {
                WorkingDirectory = workingDirectory,
                UseShellExecute = false,
                RedirectStandardOutput = true,
                RedirectStandardError = true
            };

            foreach (var argument in arguments)
            {
                startInfo.ArgumentList.Add(argument);
            }

            foreach (var (key, value) in environment)
            {
                startInfo.Environment[key] = value;
            }

            var process = Process.Start(startInfo)
                ?? throw new InvalidOperationException($"Failed to start process '{name}'.");

            return new StartedProcess(
                name,
                process,
                logPrefix + ".stdout.log",
                logPrefix + ".stderr.log");
        }

        public async Task<int> WaitForExitAndCaptureAsync(TimeSpan timeout)
        {
            using var cancellation = new CancellationTokenSource(timeout);

            try
            {
                await _process.WaitForExitAsync(cancellation.Token);
            }
            catch (OperationCanceledException)
            {
                await StopAsync();
                throw new TimeoutException(
                    $"{Name} did not exit within {timeout}. Logs: {StandardOutputPath} / {StandardErrorPath}");
            }

            await CaptureLogsAsync();
            return _process.ExitCode;
        }

        public async Task StopAsync()
        {
            if (!_process.HasExited)
            {
                _process.Kill(entireProcessTree: true);
                await _process.WaitForExitAsync().WaitAsync(TimeSpan.FromSeconds(15));
            }

            await CaptureLogsAsync();
        }

        public async Task CaptureLogsAsync()
        {
            if (_logsCaptured)
            {
                return;
            }

            await File.WriteAllTextAsync(
                StandardOutputPath,
                await CompleteLogCaptureAsync(_standardOutput, "stdout"));
            await File.WriteAllTextAsync(
                StandardErrorPath,
                await CompleteLogCaptureAsync(_standardError, "stderr"));
            _logsCaptured = true;
        }

        private static async Task<string> CompleteLogCaptureAsync(Task<string> logTask, string streamName)
        {
            try
            {
                return await logTask.WaitAsync(TimeSpan.FromSeconds(5));
            }
            catch (TimeoutException)
            {
                return $"<{streamName} capture timed out after process termination>";
            }
        }

        public void Dispose()
        {
            _process.Dispose();
        }
    }
}
