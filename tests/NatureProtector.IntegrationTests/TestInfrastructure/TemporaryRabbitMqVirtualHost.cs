using System.Diagnostics;
using System.Net;
using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using NatureProtector.Shared.Configuration;
using RabbitMQ.Client;

namespace NatureProtector.IntegrationTests.TestInfrastructure;

internal sealed class TemporaryRabbitMqVirtualHost : IAsyncDisposable
{
    private readonly RabbitMqOptions _baseOptions;
    private readonly string? _managementUrl;
    private readonly string? _dockerContainerName;
    private bool _deleted;

    private TemporaryRabbitMqVirtualHost(
        RabbitMqOptions baseOptions,
        string name,
        string? managementUrl,
        string? dockerContainerName)
    {
        _baseOptions = baseOptions;
        Name = name;
        _managementUrl = managementUrl;
        _dockerContainerName = dockerContainerName;
    }

    public string Name { get; }

    public static async Task<TemporaryRabbitMqVirtualHost> CreateAsync(
        RabbitMqOptions baseOptions,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(baseOptions);

        var name = $"np-it-{Guid.NewGuid():N}";
        var managementUrl = GetManagementUrl();
        if (managementUrl is not null &&
            await TryCreateWithManagementAsync(baseOptions, managementUrl, name, cancellationToken))
        {
            var virtualHost = new TemporaryRabbitMqVirtualHost(baseOptions, name, managementUrl, null);
            await virtualHost.WaitUntilConnectableAsync(cancellationToken);
            return virtualHost;
        }

        var dockerContainer = await FindRabbitMqContainerAsync(cancellationToken);
        if (!string.IsNullOrWhiteSpace(dockerContainer))
        {
            await RunRabbitMqCtlAsync(
                dockerContainer,
                cancellationToken,
                "add_vhost",
                name);
            await RunRabbitMqCtlAsync(
                dockerContainer,
                cancellationToken,
                "set_permissions",
                "-p",
                name,
                baseOptions.UserName,
                ".*",
                ".*",
                ".*");

            var virtualHost = new TemporaryRabbitMqVirtualHost(baseOptions, name, null, dockerContainer);
            await virtualHost.WaitUntilConnectableAsync(cancellationToken);
            return virtualHost;
        }

        throw new InvalidOperationException(
            "Could not create an isolated RabbitMQ vhost. Set NP_TEST_RABBITMQ_MANAGEMENT_URL " +
            "or NP_TEST_RABBITMQ_CONTAINER for DockerIntegration tests.");
    }

    public RabbitMqOptions CreateOptions(
        string exchangeName,
        bool? observabilityRawEnabled = null)
    {
        return DockerIntegrationSettings.CreateRabbitMqOptions(
            exchangeName,
            Name,
            $"np.it.ingestion.{Guid.NewGuid():N}",
            $"np.it.raw.{Guid.NewGuid():N}",
            observabilityRawEnabled ?? _baseOptions.ObservabilityRawEnabled);
    }

    public ConnectionFactory CreateConnectionFactory()
    {
        return new ConnectionFactory
        {
            HostName = _baseOptions.HostName,
            Port = _baseOptions.Port,
            UserName = _baseOptions.UserName,
            Password = _baseOptions.Password,
            VirtualHost = Name
        };
    }


    public async Task SetQueuePolicyAsync(
        string policyName,
        string queueName,
        IReadOnlyDictionary<string, object> definition,
        int priority = 100,
        CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(policyName);
        ArgumentException.ThrowIfNullOrWhiteSpace(queueName);
        ArgumentNullException.ThrowIfNull(definition);

        var pattern = $"^{System.Text.RegularExpressions.Regex.Escape(queueName)}$";

        if (_managementUrl is not null)
        {
            using var client = CreateManagementClient(_baseOptions, _managementUrl);
            var body = JsonSerializer.Serialize(new Dictionary<string, object>
            {
                ["pattern"] = pattern,
                ["definition"] = definition,
                ["priority"] = priority,
                ["apply-to"] = "queues"
            });
            using var response = await client.PutAsync(
                $"api/policies/{Uri.EscapeDataString(Name)}/{Uri.EscapeDataString(policyName)}",
                new StringContent(body, Encoding.UTF8, "application/json"),
                cancellationToken);
            response.EnsureSuccessStatusCode();
            return;
        }

        if (_dockerContainerName is null)
        {
            throw new InvalidOperationException(
                $"Cannot set RabbitMQ policy '{policyName}' because no management API or Docker container is available.");
        }

        await RunRabbitMqCtlAsync(
            _dockerContainerName,
            cancellationToken,
            "set_policy",
            "-p",
            Name,
            "--apply-to",
            "queues",
            "--priority",
            priority.ToString(System.Globalization.CultureInfo.InvariantCulture),
            policyName,
            pattern,
            JsonSerializer.Serialize(definition));
    }

    public async Task ClearPolicyAsync(
        string policyName,
        CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(policyName);

        if (_managementUrl is not null)
        {
            using var client = CreateManagementClient(_baseOptions, _managementUrl);
            using var response = await client.DeleteAsync(
                $"api/policies/{Uri.EscapeDataString(Name)}/{Uri.EscapeDataString(policyName)}",
                cancellationToken);
            if (response.StatusCode is HttpStatusCode.NotFound)
            {
                return;
            }

            response.EnsureSuccessStatusCode();
            return;
        }

        if (_dockerContainerName is null)
        {
            return;
        }

        await RunRabbitMqCtlAsync(
            _dockerContainerName,
            cancellationToken,
            "clear_policy",
            "-p",
            Name,
            policyName);
    }

    public async Task<bool> ExistsAsync(CancellationToken cancellationToken)
    {
        if (_managementUrl is not null)
        {
            using var client = CreateManagementClient(_baseOptions, _managementUrl);
            using var response = await client.GetAsync(
                $"api/vhosts/{Uri.EscapeDataString(Name)}",
                cancellationToken);
            return response.StatusCode == HttpStatusCode.OK;
        }

        if (_dockerContainerName is null)
        {
            return false;
        }

        var result = await RunDockerAsync(
            cancellationToken,
            "exec",
            _dockerContainerName,
            "rabbitmqctl",
            "list_vhosts",
            "name");
        return result.ExitCode == 0 &&
               result.StandardOutput
                   .Split(['\r', '\n'], StringSplitOptions.RemoveEmptyEntries)
                   .Any(line => string.Equals(line.Trim(), Name, StringComparison.Ordinal));
    }

    public async Task DeleteAsync(CancellationToken cancellationToken)
    {
        if (_deleted)
        {
            return;
        }

        _deleted = true;

        if (_managementUrl is not null)
        {
            using var client = CreateManagementClient(_baseOptions, _managementUrl);
            using var response = await client.DeleteAsync(
                $"api/vhosts/{Uri.EscapeDataString(Name)}",
                cancellationToken);
            if (response.StatusCode is HttpStatusCode.NotFound)
            {
                return;
            }

            response.EnsureSuccessStatusCode();
            return;
        }

        if (_dockerContainerName is not null)
        {
            await RunRabbitMqCtlAsync(
                _dockerContainerName,
                cancellationToken,
                "delete_vhost",
                Name);
        }
    }

    public async ValueTask DisposeAsync()
    {
        await DeleteAsync(CancellationToken.None);
    }

    private async Task WaitUntilConnectableAsync(CancellationToken cancellationToken)
    {
        var factory = CreateConnectionFactory();
        Exception? lastFailure = null;

        for (var attempt = 0; attempt < 50; attempt++)
        {
            cancellationToken.ThrowIfCancellationRequested();

            try
            {
                using var connection = factory.CreateConnection("natureprotector-docker-it-vhost-readiness");
                if (connection.IsOpen)
                {
                    return;
                }
            }
            catch (Exception ex)
            {
                lastFailure = ex;
            }

            await Task.Delay(TimeSpan.FromMilliseconds(100), cancellationToken);
        }

        throw new TimeoutException(
            $"RabbitMQ vhost '{Name}' was created but did not become connectable.",
            lastFailure);
    }

    private static string? GetManagementUrl()
    {
        var configured = Environment.GetEnvironmentVariable("NP_TEST_RABBITMQ_MANAGEMENT_URL");
        if (!string.IsNullOrWhiteSpace(configured))
        {
            return configured.TrimEnd('/') + "/";
        }

        return null;
    }

    private static async Task<bool> TryCreateWithManagementAsync(
        RabbitMqOptions options,
        string managementUrl,
        string vhostName,
        CancellationToken cancellationToken)
    {
        try
        {
            using var client = CreateManagementClient(options, managementUrl);
            using var createVhost = await client.PutAsync(
                $"api/vhosts/{Uri.EscapeDataString(vhostName)}",
                new StringContent(string.Empty),
                cancellationToken);
            if (!createVhost.IsSuccessStatusCode)
            {
                return false;
            }

            var body = JsonSerializer.Serialize(new
            {
                configure = ".*",
                write = ".*",
                read = ".*"
            });
            using var permissions = await client.PutAsync(
                $"api/permissions/{Uri.EscapeDataString(vhostName)}/{Uri.EscapeDataString(options.UserName)}",
                new StringContent(body, Encoding.UTF8, "application/json"),
                cancellationToken);
            return permissions.IsSuccessStatusCode;
        }
        catch
        {
            return false;
        }
    }

    private static HttpClient CreateManagementClient(RabbitMqOptions options, string managementUrl)
    {
        var client = new HttpClient
        {
            BaseAddress = new Uri(managementUrl),
            Timeout = TimeSpan.FromSeconds(5)
        };
        var credentials = Convert.ToBase64String(
            Encoding.ASCII.GetBytes($"{options.UserName}:{options.Password}"));
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Basic", credentials);
        return client;
    }

    private static async Task<string?> FindRabbitMqContainerAsync(CancellationToken cancellationToken)
    {
        var configured = Environment.GetEnvironmentVariable("NP_TEST_RABBITMQ_CONTAINER");
        if (!string.IsNullOrWhiteSpace(configured))
        {
            return configured;
        }

        var result = await RunDockerAsync(cancellationToken, "ps", "--format", "{{.Names}}");
        if (result.ExitCode != 0)
        {
            return null;
        }

        return result.StandardOutput
            .Split(['\r', '\n'], StringSplitOptions.RemoveEmptyEntries)
            .Select(name => name.Trim())
            .OrderByDescending(name => string.Equals(name, "np-rabbitmq-it", StringComparison.OrdinalIgnoreCase))
            .FirstOrDefault(name => name.Contains("rabbit", StringComparison.OrdinalIgnoreCase));
    }

    private static async Task RunRabbitMqCtlAsync(
        string containerName,
        CancellationToken cancellationToken,
        params string[] rabbitMqCtlArguments)
    {
        var arguments = new List<string>
        {
            "exec",
            containerName,
            "rabbitmqctl"
        };
        arguments.AddRange(rabbitMqCtlArguments);

        var result = await RunDockerAsync(cancellationToken, arguments.ToArray());
        if (result.ExitCode != 0)
        {
            throw new InvalidOperationException(
                $"rabbitmqctl failed with exit code {result.ExitCode}. " +
                $"stdout: {result.StandardOutput.Trim()} stderr: {result.StandardError.Trim()}");
        }
    }

    private static async Task<ProcessResult> RunDockerAsync(
        CancellationToken cancellationToken,
        params string[] arguments)
    {
        var startInfo = new ProcessStartInfo("docker")
        {
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            UseShellExecute = false
        };

        foreach (var argument in arguments)
        {
            startInfo.ArgumentList.Add(argument);
        }

        using var process = Process.Start(startInfo)
            ?? throw new InvalidOperationException("Failed to start docker process.");
        var stdout = process.StandardOutput.ReadToEndAsync(cancellationToken);
        var stderr = process.StandardError.ReadToEndAsync(cancellationToken);
        await process.WaitForExitAsync(cancellationToken);

        return new ProcessResult(
            process.ExitCode,
            await stdout,
            await stderr);
    }

    private sealed record ProcessResult(
        int ExitCode,
        string StandardOutput,
        string StandardError);
}
