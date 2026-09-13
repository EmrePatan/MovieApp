namespace MovieApp.Application.Caching;

public static class HomeGlobalCacheLockKeys
{
    public const string Prefix = "home-global-lock:";

    public static string Create(string cacheKey) => $"{Prefix}{cacheKey}";
}
