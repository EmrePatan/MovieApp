using MovieApp.Domain.Entities;
using MovieApp.Domain.Enums;

namespace MovieApp.Application.Services.ReleaseNotifications;

internal static class ReleaseNotificationEligibility
{
    public static bool CanFanOutSource(CatalogReleaseEventSource source) =>
        source != CatalogReleaseEventSource.BaselineAbsorb;

    public static bool MatchesPreference(CatalogReleaseEvent releaseEvent, TvShowFollow follow) =>
        releaseEvent.EventType switch
        {
            CatalogReleaseEventType.NewEpisode => follow.NotifyNewEpisodes,
            CatalogReleaseEventType.NewSeasonPremiere => follow.NotifyNewSeasons,
            _ => false
        };

    public static bool IsWithinNotificationBoundary(CatalogReleaseEvent releaseEvent, TvShowFollow follow)
    {
        if (!follow.NotifyFromUtc.HasValue)
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
            _ => throw new ArgumentOutOfRangeException(nameof(eventType), eventType, "Unsupported release event type.")
        };
}
