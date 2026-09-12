using StackExchange.Redis;

namespace MovieApp.Infrastructure.Configuration;

internal static class RedisConnectionOptionsFactory
{
    internal static ConfigurationOptions Create(RedisOptions redisOptions)
    {
        ArgumentNullException.ThrowIfNull(redisOptions);

        var configurationOptions = ParseConnectionString(redisOptions.ConnectionString);
        configurationOptions.ConnectTimeout = Math.Max(500, redisOptions.ConnectTimeoutMs);
        configurationOptions.SyncTimeout = Math.Max(500, redisOptions.SyncTimeoutMs);
        configurationOptions.AbortOnConnectFail = false;

        return configurationOptions;
    }

    internal static string CreateHealthCheckConnectionString(RedisOptions redisOptions) =>
        Create(redisOptions).ToString();

    internal static ConfigurationOptions ParseConnectionString(string connectionString)
    {
        if (string.IsNullOrWhiteSpace(connectionString))
        {
            throw new ArgumentException("Redis connection string is required.", nameof(connectionString));
        }

        var trimmed = connectionString.Trim();
        if (trimmed.Contains("://", StringComparison.Ordinal) && !IsRedisUri(trimmed))
        {
            throw new ArgumentException(
                "Unsupported Redis connection string URI format.",
                nameof(connectionString));
        }

        if (!IsRedisUri(trimmed))
        {
            var parsed = ConfigurationOptions.Parse(trimmed);
            parsed.AbortOnConnectFail = false;
            return parsed;
        }

        var schemeSeparatorIndex = trimmed.IndexOf("://", StringComparison.Ordinal);
        var scheme = trimmed[..schemeSeparatorIndex].ToLowerInvariant();

        var remainder = trimmed[(schemeSeparatorIndex + 3)..];
        var uriPart = remainder;
        string? optionsPart = null;

        var commaIndex = remainder.IndexOf(',');
        if (commaIndex >= 0)
        {
            uriPart = remainder[..commaIndex];
            optionsPart = remainder[(commaIndex + 1)..];
        }

        if (string.IsNullOrWhiteSpace(uriPart))
        {
            throw new ArgumentException("Redis URI host is missing.", nameof(connectionString));
        }

        if (!Uri.TryCreate($"{scheme}://{uriPart}", UriKind.Absolute, out var uri))
        {
            throw new ArgumentException("Redis URI is not valid.", nameof(connectionString));
        }

        if (string.IsNullOrWhiteSpace(uri.Host))
        {
            throw new ArgumentException("Redis URI host is missing.", nameof(connectionString));
        }

        var configurationOptions = new ConfigurationOptions
        {
            Ssl = scheme == "rediss",
            AbortOnConnectFail = false,
        };

        configurationOptions.EndPoints.Add(uri.Host, uri.Port > 0 ? uri.Port : 6379);

        if (!string.IsNullOrEmpty(uri.UserInfo))
        {
            var separatorIndex = uri.UserInfo.IndexOf(':');
            if (separatorIndex >= 0)
            {
                configurationOptions.User = Uri.UnescapeDataString(uri.UserInfo[..separatorIndex]);
                configurationOptions.Password = Uri.UnescapeDataString(uri.UserInfo[(separatorIndex + 1)..]);
            }
            else
            {
                configurationOptions.User = Uri.UnescapeDataString(uri.UserInfo);
            }
        }

        if (!string.IsNullOrWhiteSpace(optionsPart))
        {
            ApplyAdditionalOptions(configurationOptions, ConfigurationOptions.Parse(optionsPart));
        }

        configurationOptions.AbortOnConnectFail = false;
        return configurationOptions;
    }

    private static bool IsRedisUri(string value) =>
        value.StartsWith("redis://", StringComparison.OrdinalIgnoreCase)
        || value.StartsWith("rediss://", StringComparison.OrdinalIgnoreCase);

    private static void ApplyAdditionalOptions(
        ConfigurationOptions target,
        ConfigurationOptions additional)
    {
        if (additional.ConnectTimeout > 0)
        {
            target.ConnectTimeout = additional.ConnectTimeout;
        }

        if (additional.SyncTimeout > 0)
        {
            target.SyncTimeout = additional.SyncTimeout;
        }

        if (additional.DefaultDatabase > 0)
        {
            target.DefaultDatabase = additional.DefaultDatabase;
        }

        if (!string.IsNullOrWhiteSpace(additional.ClientName))
        {
            target.ClientName = additional.ClientName;
        }
    }
}
