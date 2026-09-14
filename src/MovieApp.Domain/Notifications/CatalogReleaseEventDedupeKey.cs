namespace MovieApp.Domain.Notifications;

public static class CatalogReleaseEventDedupeKey
{
    public static string ForEpisode(Guid tvShowId, int seasonNumber, int episodeNumber) =>
        $"tv:{tvShowId}:episode:{seasonNumber}:{episodeNumber}";

    public static string ForSeasonPremiere(Guid tvShowId, int seasonNumber) =>
        $"tv:{tvShowId}:season:{seasonNumber}:premiere";

    public static string ForMovieReleased(Guid movieId) =>
        $"movie:{movieId}:released";
}
