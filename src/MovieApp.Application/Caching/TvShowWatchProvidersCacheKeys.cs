namespace MovieApp.Application.Caching;

public static class TvShowWatchProvidersCacheKeys
{
    public const string Prefix = "tvshow-watch-providers:";

    public const string Version = "v1";

    public static string Create(Guid tvShowId, string region) =>
        $"{Prefix}{tvShowId}:{region.ToUpperInvariant()}:{Version}";
}
