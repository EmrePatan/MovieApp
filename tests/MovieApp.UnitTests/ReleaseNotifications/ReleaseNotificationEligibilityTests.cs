using MovieApp.Application.Services.ReleaseNotifications;
using MovieApp.Domain.Entities;
using MovieApp.Domain.Enums;
using MovieApp.Domain.Notifications;

namespace MovieApp.UnitTests.ReleaseNotifications;

public sealed class ReleaseNotificationEligibilityTests
{
    [Fact]
    public void CanFanOutSource_RejectsBaselineAbsorb()
    {
        Assert.False(ReleaseNotificationEligibility.CanFanOutSource(CatalogReleaseEventSource.BaselineAbsorb));
        Assert.True(ReleaseNotificationEligibility.CanFanOutSource(CatalogReleaseEventSource.BoundaryDetection));
    }

    [Fact]
    public void IsWithinNotificationBoundary_UsesDateSemanticsForSameCalendarDay()
    {
        var releaseEvent = CreateEpisodeEvent(new DateOnly(2026, 9, 15));
        var follow = CreateFollow(new DateTime(2026, 9, 15, 18, 0, 0, DateTimeKind.Utc));

        Assert.True(ReleaseNotificationEligibility.IsWithinNotificationBoundary(releaseEvent, follow));
    }

    [Fact]
    public void IsWithinNotificationBoundary_RejectsEventBeforeFollowDate()
    {
        var releaseEvent = CreateEpisodeEvent(new DateOnly(2026, 9, 14));
        var follow = CreateFollow(new DateTime(2026, 9, 15, 10, 0, 0, DateTimeKind.Utc));

        Assert.False(ReleaseNotificationEligibility.IsWithinNotificationBoundary(releaseEvent, follow));
    }

    private static CatalogReleaseEvent CreateEpisodeEvent(DateOnly airDate) =>
        CatalogReleaseEventFactory.CreateEpisodeEvent(
            Guid.NewGuid(),
            1,
            5,
            airDate,
            CatalogReleaseEventSource.BoundaryDetection,
            DateTime.UtcNow);

    private static TvShowFollow CreateFollow(DateTime notifyFromUtc)
    {
        var follow = TvShowFollow.Create(Guid.NewGuid(), Guid.NewGuid(), true, true, DateTime.UtcNow);
        follow.SetNotifyFromUtc(notifyFromUtc, DateTime.UtcNow);
        follow.EstablishBaseline(DateTime.UtcNow);
        return follow;
    }
}
