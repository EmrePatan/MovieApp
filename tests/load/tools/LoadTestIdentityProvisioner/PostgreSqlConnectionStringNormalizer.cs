using Npgsql;

namespace MovieApp.LoadTestIdentityProvisioner;

/// <summary>
/// Accepts Npgsql key/value strings or postgresql:// / postgres:// URIs (e.g. Render external DB URLs).
/// Never log inputs or outputs — they may contain credentials.
/// </summary>
public static class PostgreSqlConnectionStringNormalizer
{
    public static string Normalize(string connectionInput)
    {
        if (string.IsNullOrWhiteSpace(connectionInput))
        {
            throw new ArgumentException("Connection string is required.", nameof(connectionInput));
        }

        var trimmed = connectionInput.Trim();
        if (!IsPostgresUri(trimmed))
        {
            ValidateKeyValueConnectionString(trimmed);
            return trimmed;
        }

        if (!Uri.TryCreate(trimmed, UriKind.Absolute, out var uri))
        {
            throw new ArgumentException("Invalid PostgreSQL URI.", nameof(connectionInput));
        }

        var userInfo = uri.UserInfo;
        string username = string.Empty;
        string password = string.Empty;
        if (!string.IsNullOrEmpty(userInfo))
        {
            var colon = userInfo.IndexOf(':');
            if (colon < 0)
            {
                username = Uri.UnescapeDataString(userInfo);
            }
            else
            {
                username = Uri.UnescapeDataString(userInfo[..colon]);
                password = Uri.UnescapeDataString(userInfo[(colon + 1)..]);
            }
        }

        var database = uri.AbsolutePath.TrimStart('/');
        if (string.IsNullOrEmpty(database))
        {
            throw new ArgumentException("PostgreSQL URI must include a database name.", nameof(connectionInput));
        }

        var port = uri.Port > 0 ? uri.Port : 5432;
        var builder = new NpgsqlConnectionStringBuilder
        {
            Host = uri.Host,
            Port = port,
            Database = database,
            Username = username,
            Password = password,
            SslMode = SslMode.Require,
        };

        return builder.ConnectionString;
    }

    private static bool IsPostgresUri(string value) =>
        value.StartsWith("postgresql://", StringComparison.OrdinalIgnoreCase) ||
        value.StartsWith("postgres://", StringComparison.OrdinalIgnoreCase);

    private static void ValidateKeyValueConnectionString(string value)
    {
        try
        {
            _ = new NpgsqlConnectionStringBuilder(value);
        }
        catch (Exception ex)
        {
            throw new ArgumentException("Invalid PostgreSQL connection string.", nameof(value), ex);
        }
    }
}
