namespace MovieApp.Application.Caching;

public static class TvShowEpisodeCacheKeys
{
    public const string Prefix = "tvshow-episode:";

    public static string Create(Guid tvShowId, int seasonNumber, int episodeNumber) =>
        $"{Prefix}{tvShowId}:season:{seasonNumber}:episode:{episodeNumber}";
}
