using Npgsql;

namespace MovieApp.Infrastructure.Configuration;

internal static class PostgreSqlConnectionStringFactory
{
    /// <summary>
    /// Idle connections kept open when the connection string does not set "Minimum Pool Size".
    /// Production request logs showed roughly one in five requests paying 400-2000 ms to open a new
    /// connection (TLS handshake + auth against a small Postgres plan) after the pool was pruned.
    /// </summary>
    internal const int DefaultMinPoolSize = 4;

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

        if (!builder.ShouldSerialize("Minimum Pool Size"))
        {
            builder.MinPoolSize = Math.Min(DefaultMinPoolSize, builder.MaxPoolSize);
        }

        return builder.ConnectionString;
    }
}
