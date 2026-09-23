using MovieApp.Application.Services.PushNotifications;
using MovieApp.Domain.Entities;
using MovieApp.Domain.Enums;

namespace MovieApp.UnitTests.PushNotifications;

public sealed class PushNotificationMessageComposerTests
{
    [Fact]
    public void Compose_UsesStoredTitleAndBodyWhenPresent()
    {
        var delivery = CreateDelivery(
            title: "Breaking Bad",
            body: "2 new episodes",
            notificationType: UserReleaseNotificationType.NewEpisodes,
            eventCount: 2);

        var message = PushNotificationMessageComposer.Compose(delivery, 2);

        Assert.Equal("Breaking Bad", message.Title);
        Assert.Equal("2 new episodes", message.Body);
        Assert.Equal("tv-release", message.Data["type"]);
    }

    [Fact]
    public void Compose_BuildsAggregatedEpisodeBodyWhenMissing()
    {
        var delivery = CreateDelivery(
            title: null,
            body: null,
            notificationType: UserReleaseNotificationType.NewEpisodes,
            eventCount: 3);

        var message = PushNotificationMessageComposer.Compose(delivery, 3);

        Assert.Equal("TV Show", message.Title);
        Assert.Equal("3 new episodes", message.Body);
    }

    [Fact]
    public void Compose_BuildsSeasonBodyWhenMissing()
    {
        var delivery = CreateDelivery(
            title: "Show",
            body: null,
            notificationType: UserReleaseNotificationType.NewSeason,
            eventCount: 1);

        var message = PushNotificationMessageComposer.Compose(delivery, 1);

        Assert.Equal("New season premiere", message.Body);
    }

    [Fact]
    public void Compose_UsesDeviceContentLocaleForGeneratedBody()
    {
        var delivery = CreateDelivery(
            title: null,
            body: null,
            notificationType: UserReleaseNotificationType.NewEpisodes,
            eventCount: 3,
            contentLocale: "tr-TR");

        var message = PushNotificationMessageComposer.Compose(delivery, 3);

        Assert.Equal("Dizi", message.Title);
        Assert.Equal("3 yeni bölüm", message.Body);
    }

    private static PushNotificationDelivery CreateDelivery(
        string? title,
        string? body,
        UserReleaseNotificationType notificationType,
        int eventCount,
        string? contentLocale = null)
    {
        var notification = new UserReleaseNotification
        {
            Id = Guid.NewGuid(),
            UserId = Guid.NewGuid(),
            TvShowId = Guid.NewGuid(),
            NotificationType = notificationType,
            Title = title,
            Body = body,
            NotificationEvents = Enumerable.Range(0, eventCount)
                .Select(_ => new UserReleaseNotificationEvent())
                .ToList()
        };

        return new PushNotificationDelivery
        {
            Id = Guid.NewGuid(),
            UserReleaseNotification = notification,
            PushDevice = new PushDevice
            {
                ExpoPushToken = "ExponentPushToken[abcdefghijklmnopqrstuvwxyz123456]",
                ContentLocale = contentLocale
            }
        };
    }
}
