using MovieApp.Domain.Entities;
using MovieApp.Domain.Enums;

namespace MovieApp.Application.Services.ReleaseNotifications;

internal static class ReleaseNotificationEligibility
{
    public static bool CanFanOutSource(CatalogReleaseEventSource source) =>
        source != CatalogReleaseEventSource.BaselineAbsorb;

    public static bool MatchesPreference(CatalogReleaseEvent releaseEvent, CatalogFollow follow) =>
        releaseEvent.EventType switch
        {
            CatalogReleaseEventType.NewEpisode =>
                follow.ContentType == CatalogContentType.Tv && follow.NotifyNewEpisodes,
            CatalogReleaseEventType.NewSeasonPremiere =>
                follow.ContentType == CatalogContentType.Tv && follow.NotifyNewSeasons,
            CatalogReleaseEventType.MovieReleased =>
                follow.ContentType == CatalogContentType.Movie && follow.NotifyMovieRelease,
            _ => false
        };

    public static bool IsWithinNotificationBoundary(CatalogReleaseEvent releaseEvent, CatalogFollow follow)
    {
        if (releaseEvent.EventType == CatalogReleaseEventType.MovieReleased)
        {
            return true;
        }

        if (follow.ContentType != CatalogContentType.Tv || !follow.NotifyFromUtc.HasValue)
        {
            return false;
        }

        var releaseDate = DateOnly.FromDateTime(releaseEvent.ReleaseAtUtc);
        var notifyFromDate = DateOnly.FromDateTime(follow.NotifyFromUtc.Value);
        return releaseDate >= notifyFromDate;
    }

    public static UserReleaseNotificationType MapNotificationType(CatalogReleaseEventType eventType) =>
        eventType switch
        {
            CatalogReleaseEventType.NewEpisode => UserReleaseNotificationType.NewEpisodes,
            CatalogReleaseEventType.NewSeasonPremiere => UserReleaseNotificationType.NewSeason,
            CatalogReleaseEventType.MovieReleased => UserReleaseNotificationType.MovieReleased,
            _ => throw new ArgumentOutOfRangeException(nameof(eventType), eventType, "Unsupported release event type.")
        };
}
