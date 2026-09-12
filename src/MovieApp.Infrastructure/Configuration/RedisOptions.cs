namespace MovieApp.Infrastructure.Configuration;

public sealed class RedisOptions
{
    public const string SectionName = "Redis";

    public string ConnectionString { get; set; } = string.Empty;

    public string InstanceName { get; set; } = "MovieApp:";

    /// <summary>
    /// StackExchange.Redis connect timeout in milliseconds.
    /// Keeps startup and reconnect attempts bounded.
    /// </summary>
    public int ConnectTimeoutMs { get; set; } = 5000;

    /// <summary>
    /// StackExchange.Redis synchronous command timeout in milliseconds.
    /// Cache operations should fail fast rather than block API requests.
    /// </summary>
    public int SyncTimeoutMs { get; set; } = 2000;

    public bool IsConfigured() => !string.IsNullOrWhiteSpace(ConnectionString);
}
