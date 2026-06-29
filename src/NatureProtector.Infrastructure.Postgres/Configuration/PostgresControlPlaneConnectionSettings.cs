using Npgsql;

namespace NatureProtector.Infrastructure.Postgres.Configuration;

public sealed record PostgresControlPlaneConnectionSettings(
    string Host,
    int Port,
    string Database,
    string Username,
    string Password,
    string? SslModeName = null,
    string? RootCertificate = null)
{
    public NpgsqlDataSource BuildDataSource()
        => PostgresDataSourceFactory.Build(BuildConnectionString(), RootCertificate);

    public string BuildConnectionString()
    {
        var builder = new NpgsqlConnectionStringBuilder
        {
            Host = Host,
            Port = Port,
            Database = Database,
            Username = Username,
            Password = Password,
            IncludeErrorDetail = true
        };

        if (!string.IsNullOrWhiteSpace(SslModeName))
        {
            var sslMode = Enum.Parse<SslMode>(SslModeName, ignoreCase: true);
            builder.SslMode = sslMode;
            if (sslMode is SslMode.VerifyCA or SslMode.VerifyFull)
            {
                builder.ChannelBinding = ChannelBinding.Require;
            }
        }

        if (!string.IsNullOrWhiteSpace(RootCertificate))
        {
            builder.RootCertificate = RootCertificate;
        }

        return builder.ConnectionString;
    }
}
