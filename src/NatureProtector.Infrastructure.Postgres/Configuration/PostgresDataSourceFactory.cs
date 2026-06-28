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

        var dataSourceBuilder = new NpgsqlDataSourceBuilder(connectionBuilder.ConnectionString);

        if (!string.IsNullOrWhiteSpace(rootCertificatePath))
        {
            dataSourceBuilder.UseRootCertificate(
                X509CertificateLoader.LoadCertificateFromFile(rootCertificatePath));
        }

        return dataSourceBuilder.Build();
    }
}
