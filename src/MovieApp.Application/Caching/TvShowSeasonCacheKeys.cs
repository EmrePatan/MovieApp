namespace MovieApp.Application.Caching;

public static class TvShowSeasonCacheKeys
{
    public const string Prefix = "tvshow-season:";

    public static string Create(Guid tvShowId, int seasonNumber) =>
        $"{Prefix}{tvShowId}:season:{seasonNumber}";
}
