namespace MovieApp.Application.Caching;

public static class DiscoveryCacheLockKeys
{
    public const string Prefix = "discovery-cache-lock:";

    public static string Create(string cacheKey) => $"{Prefix}{cacheKey}";
}
