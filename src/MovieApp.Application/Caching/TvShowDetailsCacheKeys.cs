namespace MovieApp.Application.Caching;

public static class TvShowDetailsCacheKeys
{
    public const string Prefix = "tvshow-details:";

    public static string Create(Guid tvShowId) => $"{Prefix}{tvShowId}";
}
