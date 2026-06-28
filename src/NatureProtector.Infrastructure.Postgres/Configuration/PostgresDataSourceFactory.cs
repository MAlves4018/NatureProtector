using System.Net.Security;
using System.Security.Cryptography.X509Certificates;
using Npgsql;

namespace NatureProtector.Infrastructure.Postgres.Configuration;

public static class PostgresDataSourceFactory
{
    public static NpgsqlDataSource Build(string connectionString, string? rootCertificatePath)
    {
        var connectionBuilder = new NpgsqlConnectionStringBuilder(connectionString);

        if (!string.IsNullOrWhiteSpace(rootCertificatePath))
        {
            connectionBuilder.RootCertificate = null;
        }

        var rootCertificate = string.IsNullOrWhiteSpace(rootCertificatePath)
            ? null
            : X509CertificateLoader.LoadCertificateFromFile(rootCertificatePath);

        var validateCertificateAuthorityOnly = rootCertificate is not null &&
            connectionBuilder.SslMode == SslMode.VerifyCA;

        if (validateCertificateAuthorityOnly)
        {
            connectionBuilder.SslMode = SslMode.Require;
        }

        var dataSourceBuilder = new NpgsqlDataSourceBuilder(connectionBuilder.ConnectionString);

        if (rootCertificate is not null)
        {
            if (validateCertificateAuthorityOnly)
            {
                dataSourceBuilder.UseSslClientAuthenticationOptionsCallback(
                    options =>
                    {
                        options.RemoteCertificateValidationCallback = (_, certificate, _, _) =>
                            ValidateServerCertificateForCertificateAuthority(certificate, rootCertificate);
                    });
            }
            else
            {
                dataSourceBuilder.UseRootCertificate(rootCertificate);
            }
        }

        return dataSourceBuilder.Build();
    }

    public static bool ValidateServerCertificateForCertificateAuthority(
        X509Certificate? certificate,
        X509Certificate2 certificateAuthority)
    {
        if (certificate is null)
        {
            return false;
        }

        using var serverCertificate = certificate as X509Certificate2 ?? new X509Certificate2(certificate);
        using var chain = new X509Chain();
        chain.ChainPolicy.TrustMode = X509ChainTrustMode.CustomRootTrust;
        chain.ChainPolicy.CustomTrustStore.Add(certificateAuthority);
        chain.ChainPolicy.RevocationMode = X509RevocationMode.NoCheck;
        chain.ChainPolicy.VerificationFlags = X509VerificationFlags.NoFlag;

        return chain.Build(serverCertificate);
    }
}
