using MovieApp.Domain.Entities;
using MovieApp.Domain.Enums;

namespace MovieApp.Domain.Notifications;

public static class CatalogReleaseEventFactory
{
    public static CatalogReleaseEvent CreateEpisodeEvent(
        Guid tvShowId,
        int seasonNumber,
        int episodeNumber,
        DateOnly airDate,
        CatalogReleaseEventSource source,
        DateTime detectedAtUtc) =>
        new()
        {
            Id = Guid.NewGuid(),
            TvShowId = tvShowId,
            EventType = CatalogReleaseEventType.NewEpisode,
            SeasonNumber = seasonNumber,
            EpisodeNumber = episodeNumber,
            ReleaseAtUtc = ReleaseDateTime.ToReleaseAtUtc(airDate),
            DetectedAtUtc = detectedAtUtc,
            Source = source,
            DedupeKey = CatalogReleaseEventDedupeKey.ForEpisode(tvShowId, seasonNumber, episodeNumber)
        };

    public static CatalogReleaseEvent CreateSeasonPremiereEvent(
        Guid tvShowId,
        int seasonNumber,
        DateOnly premiereDate,
        CatalogReleaseEventSource source,
        DateTime detectedAtUtc) =>
        new()
        {
            Id = Guid.NewGuid(),
            TvShowId = tvShowId,
            EventType = CatalogReleaseEventType.NewSeasonPremiere,
            SeasonNumber = seasonNumber,
            EpisodeNumber = null,
            ReleaseAtUtc = ReleaseDateTime.ToReleaseAtUtc(premiereDate),
            DetectedAtUtc = detectedAtUtc,
            Source = source,
            DedupeKey = CatalogReleaseEventDedupeKey.ForSeasonPremiere(tvShowId, seasonNumber)
        };

    public static CatalogReleaseEvent CreateMovieReleasedEvent(
        Guid movieId,
        DateOnly releaseDate,
        CatalogReleaseEventSource source,
        DateTime detectedAtUtc) =>
        new()
        {
            Id = Guid.NewGuid(),
            MovieId = movieId,
            EventType = CatalogReleaseEventType.MovieReleased,
            SeasonNumber = 0,
            EpisodeNumber = null,
            ReleaseAtUtc = ReleaseDateTime.ToReleaseAtUtc(releaseDate),
            DetectedAtUtc = detectedAtUtc,
            Source = source,
            DedupeKey = CatalogReleaseEventDedupeKey.ForMovieReleased(movieId)
        };
}
