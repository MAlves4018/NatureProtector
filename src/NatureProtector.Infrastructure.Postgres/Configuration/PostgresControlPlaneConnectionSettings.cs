using Npgsql;

namespace NatureProtector.Infrastructure.Postgres.Configuration;

public sealed record PostgresControlPlaneConnectionSettings(
    string Host,
    int Port,
    string Database,
    string Username,
    string Password)
{
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

        return builder.ConnectionString;
    }
}
