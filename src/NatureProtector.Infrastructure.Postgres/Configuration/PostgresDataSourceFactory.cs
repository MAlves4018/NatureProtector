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
            connectionBuilder["Trust Server Certificate"] = false;
        }

        var rootCertificate = string.IsNullOrWhiteSpace(rootCertificatePath)
            ? null
            : X509CertificateLoader.LoadCertificateFromFile(rootCertificatePath);

        var dataSourceBuilder = new NpgsqlDataSourceBuilder(connectionBuilder.ConnectionString);

        if (rootCertificate is not null)
        {
            dataSourceBuilder.UseSslClientAuthenticationOptionsCallback(
                options =>
                {
                    options.CertificateChainPolicy = CreateCertificateChainPolicy(rootCertificate);
                });
        }

        return dataSourceBuilder.Build();
    }

    private static X509ChainPolicy CreateCertificateChainPolicy(X509Certificate2 certificateAuthority)
    {
        var policy = new X509ChainPolicy
        {
            TrustMode = X509ChainTrustMode.CustomRootTrust,
            RevocationMode = X509RevocationMode.NoCheck,
            VerificationFlags = X509VerificationFlags.NoFlag
        };
        policy.CustomTrustStore.Add(certificateAuthority);
        return policy;
    }

    public static bool ValidateServerCertificateForCertificateAuthority(
        X509Certificate? certificate,
        X509Certificate2 certificateAuthority)
    {
        if (certificate is null)
        {
            return false;
        }

        var ownsServerCertificate = certificate is not X509Certificate2;
        var serverCertificate = certificate as X509Certificate2 ??
            X509CertificateLoader.LoadCertificate(certificate.Export(X509ContentType.Cert));

        try
        {
            using var chain = new X509Chain();
            chain.ChainPolicy.TrustMode = X509ChainTrustMode.CustomRootTrust;
            chain.ChainPolicy.CustomTrustStore.Add(certificateAuthority);
            chain.ChainPolicy.RevocationMode = X509RevocationMode.NoCheck;
            chain.ChainPolicy.VerificationFlags = X509VerificationFlags.NoFlag;

            return chain.Build(serverCertificate);
        }
        finally
        {
            if (ownsServerCertificate)
            {
                serverCertificate.Dispose();
            }
        }
    }
}
