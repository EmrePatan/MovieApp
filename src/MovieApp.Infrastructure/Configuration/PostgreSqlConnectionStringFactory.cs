using Npgsql;

namespace MovieApp.Infrastructure.Configuration;

internal static class PostgreSqlConnectionStringFactory
{
    internal static string Normalize(string connectionString)
    {
        if (string.IsNullOrWhiteSpace(connectionString))
        {
            throw new ArgumentException("PostgreSQL connection string is required.", nameof(connectionString));
        }

        var builder = new NpgsqlConnectionStringBuilder(connectionString.Trim())
        {
            GssEncryptionMode = GssEncryptionMode.Disable,
        };

        return builder.ConnectionString;
    }
}
