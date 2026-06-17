using System.Diagnostics;
using NatureProtector.Infrastructure.Influx.Configuration;

namespace NatureProtector.IntegrationTests.TestInfrastructure;

internal sealed class TemporaryInfluxDatabase : IAsyncDisposable
{
    private const string ContainerHostUrl = "http://127.0.0.1:8181";
    private readonly string _containerName;
    private readonly string _token;
    private bool _deleted;

    private TemporaryInfluxDatabase(string name, string containerName, string token)
    {
        Name = name;
        _containerName = containerName;
        _token = token;
    }

    public string Name { get; }

    public static async Task<TemporaryInfluxDatabase> CreateAsync(
        CancellationToken cancellationToken = default)
    {
        var name = $"np_it_{Guid.NewGuid():N}";
        var options = DockerIntegrationSettings.CreateInfluxDbOptions(name);
        var containerName = GetContainerName();
        TemporaryInfluxDatabase? database = null;

        try
        {
            await RunInfluxDb3Async(
                containerName,
                options.Token,
                cancellationToken,
                "create",
                "database",
                "--host",
                ContainerHostUrl,
                "--token",
                options.Token,
                name);

            database = new TemporaryInfluxDatabase(name, containerName, options.Token);
            return database;
        }
        catch
        {
            if (database is not null)
            {
                await database.DisposeAsync();
            }
            else
            {
                await DeleteIfExistsAsync(containerName, options.Token, name, CancellationToken.None);
            }

            throw;
        }
    }

    public InfluxDbOptions CreateOptions()
    {
        return DockerIntegrationSettings.CreateInfluxDbOptions(Name);
    }

    public async Task<bool> ExistsAsync(CancellationToken cancellationToken = default)
    {
        return await DatabaseExistsAsync(_containerName, _token, Name, cancellationToken);
    }

    public async ValueTask DisposeAsync()
    {
        if (_deleted)
        {
            return;
        }

        _deleted = true;
        await DeleteIfExistsAsync(_containerName, _token, Name, CancellationToken.None);
    }

    public static async Task<bool> DatabaseExistsAsync(
        string name,
        CancellationToken cancellationToken = default)
    {
        var options = DockerIntegrationSettings.CreateInfluxDbOptions(name);
        return await DatabaseExistsAsync(GetContainerName(), options.Token, name, cancellationToken);
    }

    private static async Task<bool> DatabaseExistsAsync(
        string containerName,
        string token,
        string name,
        CancellationToken cancellationToken)
    {
        var result = await RunInfluxDb3Async(
            containerName,
            token,
            cancellationToken,
            "show",
            "databases",
            "--host",
            ContainerHostUrl,
            "--token",
            token,
            "--format",
            "csv");

        return result.StandardOutput
            .Split(['\r', '\n'], StringSplitOptions.RemoveEmptyEntries)
            .Select(line => line.Trim())
            .Any(line => line.Contains(name, StringComparison.Ordinal));
    }

    private static async Task DeleteIfExistsAsync(
        string containerName,
        string token,
        string name,
        CancellationToken cancellationToken)
    {
        var arguments = new List<string>
        {
            "exec",
            "-i",
            "-e",
            $"INFLUXDB3_AUTH_TOKEN={token}",
            containerName,
            "script",
            "-q",
            "-c",
            $"influxdb3 delete database --host {ContainerHostUrl} --hard-delete now {name}",
            "/dev/null"
        };

        var result = await RunDockerAsync(cancellationToken, "yes\n", arguments.ToArray());
        if (result.ExitCode == 0 ||
            ContainsNotFound(result.StandardOutput) ||
            ContainsNotFound(result.StandardError))
        {
            return;
        }

        throw new InvalidOperationException(
            $"influxdb3 delete database failed with exit code {result.ExitCode}. " +
            $"stdout: {Sanitize(result.StandardOutput, token).Trim()} " +
            $"stderr: {Sanitize(result.StandardError, token).Trim()}");

        static bool ContainsNotFound(string value)
        {
            return value.Contains("not found", StringComparison.OrdinalIgnoreCase) ||
                   value.Contains("does not exist", StringComparison.OrdinalIgnoreCase);
        }
    }

    private static string GetContainerName()
    {
        return Environment.GetEnvironmentVariable("NP_TEST_INFLUXDB_CONTAINER") is { Length: > 0 } configured
            ? configured
            : "np-influxdb";
    }

    private static async Task<ProcessResult> RunInfluxDb3Async(
        string containerName,
        string token,
        CancellationToken cancellationToken,
        params string[] influxDb3Arguments)
    {
        var arguments = new List<string>
        {
            "exec",
            containerName,
            "influxdb3"
        };
        arguments.AddRange(influxDb3Arguments);

        var result = await RunDockerAsync(cancellationToken, arguments.ToArray());
        if (result.ExitCode != 0)
        {
            throw new InvalidOperationException(
                $"influxdb3 failed with exit code {result.ExitCode}. " +
                $"stdout: {Sanitize(result.StandardOutput, token).Trim()} " +
                $"stderr: {Sanitize(result.StandardError, token).Trim()}");
        }

        return result;
    }

    private static async Task<ProcessResult> RunDockerAsync(
        CancellationToken cancellationToken,
        params string[] arguments)
    {
        return await RunDockerAsync(cancellationToken, standardInput: null, arguments);
    }

    private static async Task<ProcessResult> RunDockerAsync(
        CancellationToken cancellationToken,
        string? standardInput,
        params string[] arguments)
    {
        var startInfo = new ProcessStartInfo("docker")
        {
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            RedirectStandardInput = standardInput is not null,
            UseShellExecute = false
        };

        foreach (var argument in arguments)
        {
            startInfo.ArgumentList.Add(argument);
        }

        using var process = Process.Start(startInfo)
            ?? throw new InvalidOperationException("Failed to start docker process.");
        if (standardInput is not null)
        {
            await process.StandardInput.WriteAsync(standardInput.AsMemory(), cancellationToken);
            process.StandardInput.Close();
        }

        var stdout = process.StandardOutput.ReadToEndAsync(cancellationToken);
        var stderr = process.StandardError.ReadToEndAsync(cancellationToken);
        await process.WaitForExitAsync(cancellationToken);

        return new ProcessResult(
            process.ExitCode,
            await stdout,
            await stderr);
    }

    private static string Sanitize(string value, string token)
    {
        return string.IsNullOrEmpty(token)
            ? value
            : value.Replace(token, "[redacted]", StringComparison.Ordinal);
    }

    private sealed record ProcessResult(
        int ExitCode,
        string StandardOutput,
        string StandardError);
}
