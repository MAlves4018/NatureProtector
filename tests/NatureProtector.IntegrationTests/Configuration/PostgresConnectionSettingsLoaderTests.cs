using NatureProtector.Infrastructure.Postgres.Configuration;
using System.Security.Cryptography;
using System.Security.Cryptography.X509Certificates;

namespace NatureProtector.IntegrationTests.Configuration;

[Collection("EnvironmentVariables")]
public sealed class PostgresConnectionSettingsLoaderTests : IDisposable
{
    private static readonly string[] Keys =
    [
        "POSTGRES_REQUIRE_EXPLICIT",
        "POSTGRES_HOST",
        "POSTGRES_PORT",
        "POSTGRES_DB",
        "POSTGRES_USER",
        "POSTGRES_PASSWORD",
        "POSTGRES_SSL_MODE",
        "POSTGRES_ROOT_CERTIFICATE"
    ];

    private readonly string _temporaryDirectory = Path.Combine(
        Path.GetTempPath(),
        $"natureprotector-postgres-config-{Guid.NewGuid():N}");

    public PostgresConnectionSettingsLoaderTests()
    {
        Directory.CreateDirectory(_temporaryDirectory);
        ClearEnvironment();
    }

    [Fact]
    public void LoadFromEnvironmentOrDotEnv_WhenStrictAndValuesMissing_ThrowsWithoutSecretValues()
    {
        Environment.SetEnvironmentVariable("POSTGRES_REQUIRE_EXPLICIT", "true");

        var exception = Assert.Throws<InvalidOperationException>(() =>
            PostgresConnectionSettingsLoader.LoadFromEnvironmentOrDotEnv(_temporaryDirectory));

        Assert.Contains("POSTGRES_HOST", exception.Message, StringComparison.Ordinal);
        Assert.Contains("POSTGRES_PASSWORD", exception.Message, StringComparison.Ordinal);
        Assert.DoesNotContain("np_dev_pass", exception.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void LoadFromEnvironmentOrDotEnv_WhenStrictAndExplicit_ReturnsConfiguredValues()
    {
        Environment.SetEnvironmentVariable("POSTGRES_REQUIRE_EXPLICIT", "true");
        Environment.SetEnvironmentVariable("POSTGRES_HOST", "10.20.0.10");
        Environment.SetEnvironmentVariable("POSTGRES_PORT", "5432");
        Environment.SetEnvironmentVariable("POSTGRES_DB", "natureprotector");
        Environment.SetEnvironmentVariable("POSTGRES_USER", "np_runtime");
        Environment.SetEnvironmentVariable("POSTGRES_PASSWORD", "secret-from-runtime");
        Environment.SetEnvironmentVariable("POSTGRES_SSL_MODE", "VerifyCA");
        Environment.SetEnvironmentVariable("POSTGRES_ROOT_CERTIFICATE", "/var/run/secrets/cloudsql/server-ca.pem");

        var settings = PostgresConnectionSettingsLoader.LoadFromEnvironmentOrDotEnv(_temporaryDirectory);

        Assert.Equal("10.20.0.10", settings.Host);
        Assert.Equal(5432, settings.Port);
        Assert.Equal("natureprotector", settings.Database);
        Assert.Equal("np_runtime", settings.Username);
        Assert.Equal("secret-from-runtime", settings.Password);
        Assert.Equal("VerifyCA", settings.SslModeName);
        Assert.Equal("/var/run/secrets/cloudsql/server-ca.pem", settings.RootCertificate);
        Assert.Contains("SSL Mode=VerifyCA", settings.BuildConnectionString(), StringComparison.OrdinalIgnoreCase);
        Assert.Contains("Channel Binding=Require", settings.BuildConnectionString(), StringComparison.OrdinalIgnoreCase);
        Assert.Contains("Root Certificate=/var/run/secrets/cloudsql/server-ca.pem", settings.BuildConnectionString(), StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void BuildDataSource_LoadsConfiguredRootCertificate()
    {
        var certificatePath = WriteTemporaryRootCertificate();
        try
        {
            var settings = new PostgresControlPlaneConnectionSettings(
                "10.20.0.10",
                5432,
                "natureprotector",
                "np_app",
                "password",
                "VerifyCA",
                certificatePath);

            using var dataSource = settings.BuildDataSource();

            Assert.NotNull(dataSource);
        }
        finally
        {
            File.Delete(certificatePath);
        }
    }

    [Fact]
    public void ValidateServerCertificateForCertificateAuthority_AcceptsServerCertificateSignedByConfiguredRoot()
    {
        using var root = CreateCertificateAuthority("NatureProtector Trusted Test Root");
        using var server = CreateServerCertificate(root, "Cloud SQL Server");

        var accepted = PostgresDataSourceFactory.ValidateServerCertificateForCertificateAuthority(server, root);

        Assert.True(accepted);
    }

    [Fact]
    public void ValidateServerCertificateForCertificateAuthority_DoesNotDisposeProvidedCertificate()
    {
        using var root = CreateCertificateAuthority("NatureProtector Trusted Test Root");
        using var server = CreateServerCertificate(root, "Cloud SQL Server");

        var accepted = PostgresDataSourceFactory.ValidateServerCertificateForCertificateAuthority(server, root);
        var signatureAlgorithm = server.SignatureAlgorithm.FriendlyName;

        Assert.True(accepted);
        Assert.False(string.IsNullOrWhiteSpace(signatureAlgorithm));
    }

    [Fact]
    public void ValidateServerCertificateForCertificateAuthority_CanBeInvokedRepeatedlyWithSameCertificate()
    {
        using var root = CreateCertificateAuthority("NatureProtector Trusted Test Root");
        using var server = CreateServerCertificate(root, "Cloud SQL Server");

        for (var attempt = 0; attempt < 5; attempt++)
        {
            Assert.True(PostgresDataSourceFactory.ValidateServerCertificateForCertificateAuthority(server, root));
        }

        Assert.False(string.IsNullOrWhiteSpace(server.GetSerialNumberString()));
    }

    [Fact]
    public void ValidateServerCertificateForCertificateAuthority_DoesNotDisposeBaseCertificate()
    {
        using var root = CreateCertificateAuthority("NatureProtector Trusted Test Root");
        using var server = CreateServerCertificate(root, "Cloud SQL Server");
#pragma warning disable SYSLIB0057
        using var runtimeCertificate = new X509Certificate(server.Export(X509ContentType.Cert));
#pragma warning restore SYSLIB0057

        var accepted = PostgresDataSourceFactory.ValidateServerCertificateForCertificateAuthority(runtimeCertificate, root);
        var serialNumber = runtimeCertificate.GetSerialNumberString();

        Assert.True(accepted);
        Assert.False(string.IsNullOrWhiteSpace(serialNumber));
    }

    [Fact]
    public void ValidateServerCertificateForCertificateAuthority_RejectsServerCertificateSignedByDifferentRoot()
    {
        using var trustedRoot = CreateCertificateAuthority("NatureProtector Trusted Test Root");
        using var otherRoot = CreateCertificateAuthority("NatureProtector Other Test Root");
        using var server = CreateServerCertificate(otherRoot, "Cloud SQL Server");

        var accepted = PostgresDataSourceFactory.ValidateServerCertificateForCertificateAuthority(server, trustedRoot);

        Assert.False(accepted);
    }

    [Fact]
    public void ValidateServerCertificateForCertificateAuthority_RejectsMissingServerCertificate()
    {
        using var root = CreateCertificateAuthority("NatureProtector Trusted Test Root");

        var accepted = PostgresDataSourceFactory.ValidateServerCertificateForCertificateAuthority(null, root);

        Assert.False(accepted);
        Assert.False(string.IsNullOrWhiteSpace(root.Thumbprint));
    }

    [Fact]
    public void ValidateServerCertificateForCertificateAuthority_DoesNotDisposeConfiguredCertificateAuthority()
    {
        using var root = CreateCertificateAuthority("NatureProtector Trusted Test Root");
        using var server = CreateServerCertificate(root, "Cloud SQL Server");

        Assert.True(PostgresDataSourceFactory.ValidateServerCertificateForCertificateAuthority(server, root));

        Assert.Contains("NatureProtector Trusted Test Root", root.Subject, StringComparison.Ordinal);
        Assert.False(string.IsNullOrWhiteSpace(root.GetCertHashString()));
    }

    [Fact]
    public void LoadFromEnvironmentOrDotEnv_WhenStrictPortInvalid_Throws()
    {
        Environment.SetEnvironmentVariable("POSTGRES_REQUIRE_EXPLICIT", "true");
        Environment.SetEnvironmentVariable("POSTGRES_HOST", "postgres");
        Environment.SetEnvironmentVariable("POSTGRES_PORT", "70000");
        Environment.SetEnvironmentVariable("POSTGRES_DB", "natureprotector");
        Environment.SetEnvironmentVariable("POSTGRES_USER", "np_runtime");
        Environment.SetEnvironmentVariable("POSTGRES_PASSWORD", "secret-from-runtime");

        var exception = Assert.Throws<InvalidOperationException>(() =>
            PostgresConnectionSettingsLoader.LoadFromEnvironmentOrDotEnv(_temporaryDirectory));

        Assert.Contains("between 1 and 65535", exception.Message, StringComparison.Ordinal);
    }

    public void Dispose()
    {
        ClearEnvironment();
        if (Directory.Exists(_temporaryDirectory))
        {
            Directory.Delete(_temporaryDirectory, recursive: true);
        }
    }

    private static void ClearEnvironment()
    {
        foreach (var key in Keys)
        {
            Environment.SetEnvironmentVariable(key, null);
        }
    }

    private static string WriteTemporaryRootCertificate()
    {
        using var certificate = CreateCertificateAuthority("NatureProtector Test Root");

        var path = Path.Combine(Path.GetTempPath(), $"{Guid.NewGuid():N}.pem");
        File.WriteAllText(path, certificate.ExportCertificatePem());
        return path;
    }

    private static X509Certificate2 CreateCertificateAuthority(string commonName)
    {
        using var key = RSA.Create(2048);
        var request = new CertificateRequest(
            $"CN={commonName}",
            key,
            HashAlgorithmName.SHA256,
            RSASignaturePadding.Pkcs1);

        request.CertificateExtensions.Add(
            new X509BasicConstraintsExtension(true, false, 0, true));
        request.CertificateExtensions.Add(
            new X509KeyUsageExtension(X509KeyUsageFlags.KeyCertSign | X509KeyUsageFlags.CrlSign, true));
        request.CertificateExtensions.Add(
            new X509SubjectKeyIdentifierExtension(request.PublicKey, false));

        return request.CreateSelfSigned(
            DateTimeOffset.UtcNow.AddMinutes(-1),
            DateTimeOffset.UtcNow.AddDays(1));
    }

    private static X509Certificate2 CreateServerCertificate(X509Certificate2 issuer,string commonName)
    {
        using var key = RSA.Create(2048);
        var request = new CertificateRequest(
            $"CN={commonName}",
            key,
            HashAlgorithmName.SHA256,
            RSASignaturePadding.Pkcs1);

        request.CertificateExtensions.Add(
            new X509BasicConstraintsExtension(false, false, 0, true));
        request.CertificateExtensions.Add(
            new X509KeyUsageExtension(
                X509KeyUsageFlags.DigitalSignature |
                X509KeyUsageFlags.KeyEncipherment,
                true));
        request.CertificateExtensions.Add(
            new X509SubjectKeyIdentifierExtension(request.PublicKey, false));

        var notBefore = DateTimeOffset.UtcNow.AddMinutes(-1);
        var notAfter = new DateTimeOffset(
            issuer.NotAfter.ToUniversalTime()).AddMinutes(-1);

        if (notAfter <= notBefore)
        {
            throw new InvalidOperationException(
                "The issuer certificate validity window is too short.");
        }

        return request.Create(
            issuer,
            notBefore,
            notAfter,
            Guid.NewGuid().ToByteArray());
    }
}

[CollectionDefinition("EnvironmentVariables", DisableParallelization = true)]
public sealed class EnvironmentVariablesCollection;
