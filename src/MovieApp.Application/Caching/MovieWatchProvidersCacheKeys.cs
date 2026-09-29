namespace MovieApp.Application.Caching;

public static class MovieWatchProvidersCacheKeys
{
    public const string Prefix = "movie-watch-providers:";

    public const string Version = "v3";

    public static string Create(Guid movieId, string region) =>
        $"{Prefix}{movieId}:{region.ToUpperInvariant()}:{Version}";
}
