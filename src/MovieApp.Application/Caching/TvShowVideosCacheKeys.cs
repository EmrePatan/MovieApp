namespace MovieApp.Application.Caching;

public static class TvShowVideosCacheKeys
{
    public const string Prefix = "tvshow-videos:";

    public const string Version = "v1";

    public static string Create(Guid tvShowId) => $"{Prefix}{tvShowId}:{Version}";
}
