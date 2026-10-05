namespace MovieApp.Application.Caching;

public static class HomeCacheLockKeys
{
    public const string Prefix = "home-lock:";

    public static string Create(string cacheKey) => $"{Prefix}{cacheKey}";
}
