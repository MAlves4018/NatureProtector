using System.Security.Cryptography;
using System.Security.Cryptography.X509Certificates;
using NatureProtector.Backoffice.Api.Configuration;
using NatureProtector.Shared.Configuration;

namespace NatureProtector.Backoffice.Api.Tests;

public sealed class RabbitMqManagementOptionsValidatorTests : IDisposable
{
    private readonly string _temporaryDirectory = Path.Combine(
        Path.GetTempPath(),
        $"natureprotector-rabbitmq-management-options-{Guid.NewGuid():N}");

    [Fact]
    public void Validate_accepts_https_with_a_loadable_private_ca()
    {
        var caPath = WriteRootCertificate();
        var result = new RabbitMqManagementOptionsValidator().Validate(
            null,
            ValidHttps(caPath));

        Assert.True(result.Succeeded);
    }

    [Fact]
    public void Validate_accepts_explicit_local_http_without_a_ca()
    {
        var options = new RabbitMqOptions
        {
            ManagementScheme = "http",
            ManagementAllowInsecureHttp = true
        };

        var result = new RabbitMqManagementOptionsValidator().Validate(null, options);

        Assert.True(result.Succeeded);
    }

    [Theory]
    [InlineData("ftp")]
    [InlineData("")]
    public void Validate_rejects_unsupported_or_empty_scheme(string scheme)
    {
        var result = new RabbitMqManagementOptionsValidator().Validate(
            null,
            new RabbitMqOptions { ManagementScheme = scheme });

        Assert.True(result.Failed);
        Assert.Contains(result.Failures!, failure => failure.Contains("ManagementScheme", StringComparison.Ordinal));
    }

    [Fact]
    public void Validate_rejects_http_without_explicit_insecure_opt_in()
    {
        var result = new RabbitMqManagementOptionsValidator().Validate(
            null,
            new RabbitMqOptions { ManagementScheme = "http" });

        Assert.True(result.Failed);
        Assert.Contains(result.Failures!, failure => failure.Contains("ManagementAllowInsecureHttp", StringComparison.Ordinal));
    }

    [Fact]
    public void Validate_rejects_a_private_ca_on_http()
    {
        var result = new RabbitMqManagementOptionsValidator().Validate(
            null,
            new RabbitMqOptions
            {
                ManagementScheme = "http",
                ManagementAllowInsecureHttp = true,
                ManagementCertificateAuthorityPath = "ca.pem"
            });

        Assert.True(result.Failed);
        Assert.Contains(result.Failures!, failure => failure.Contains("can only be used", StringComparison.Ordinal));
    }

    [Fact]
    public void Validate_fails_closed_when_https_private_ca_is_missing()
    {
        var result = new RabbitMqManagementOptionsValidator().Validate(
            null,
            ValidHttps(Path.Combine(_temporaryDirectory, "missing.pem")));

        Assert.True(result.Failed);
        Assert.Contains(result.Failures!, failure => failure.Contains("could not be loaded", StringComparison.Ordinal));
    }

    [Fact]
    public void Validate_fails_closed_when_https_private_ca_is_invalid()
    {
        Directory.CreateDirectory(_temporaryDirectory);
        var invalidPath = Path.Combine(_temporaryDirectory, "invalid.pem");
        File.WriteAllText(invalidPath, "not a certificate");

        var result = new RabbitMqManagementOptionsValidator().Validate(null, ValidHttps(invalidPath));

        Assert.True(result.Failed);
        Assert.Contains(result.Failures!, failure => failure.Contains("could not be loaded", StringComparison.Ordinal));
    }

    public void Dispose()
    {
        if (Directory.Exists(_temporaryDirectory))
        {
            Directory.Delete(_temporaryDirectory, recursive: true);
        }
    }

    private RabbitMqOptions ValidHttps(string caPath)
        => new()
        {
            ManagementScheme = "https",
            ManagementPort = 15671,
            ManagementCertificateAuthorityPath = caPath,
            ManagementTimeoutSeconds = 5
        };

    private string WriteRootCertificate()
    {
        Directory.CreateDirectory(_temporaryDirectory);
        using var key = RSA.Create(2048);
        var request = new CertificateRequest(
            "CN=NatureProtector RabbitMQ Management Test Root",
            key,
            HashAlgorithmName.SHA256,
            RSASignaturePadding.Pkcs1);
        request.CertificateExtensions.Add(new X509BasicConstraintsExtension(true, false, 0, true));
        request.CertificateExtensions.Add(new X509KeyUsageExtension(X509KeyUsageFlags.KeyCertSign, true));
        using var certificate = request.CreateSelfSigned(
            DateTimeOffset.UtcNow.AddMinutes(-5),
            DateTimeOffset.UtcNow.AddDays(1));
        var path = Path.Combine(_temporaryDirectory, "root.pem");
        File.WriteAllText(path, certificate.ExportCertificatePem());
        return path;
    }
}
