using MovieApp.Api.Mapping;
using MovieApp.Application.Models.Notifications;
using MovieApp.Domain.Enums;

namespace MovieApp.UnitTests.Notifications;

public sealed class NotificationContractMapperTests
{
    [Fact]
    public void ToNotificationsResponse_MapsAllNotificationTypes()
    {
        var result = new NotificationsListResult(
            [
                CreateItem(UserReleaseNotificationType.MovieReleased, "movie"),
                CreateItem(UserReleaseNotificationType.NewSeason, "tv"),
                CreateItem(UserReleaseNotificationType.NewEpisodes, "tv"),
            ],
            1,
            20,
            3,
            1);

        var response = NotificationContractMapper.ToNotificationsResponse(result);

        Assert.Equal(3, response.Items.Count);
        Assert.Equal("MovieReleased", response.Items[0].Type);
        Assert.Equal("NewSeason", response.Items[1].Type);
        Assert.Equal("NewEpisodes", response.Items[2].Type);
        Assert.Equal("movie", response.Items[0].ContentType);
    }

    private static NotificationInboxItemResult CreateItem(
        UserReleaseNotificationType type,
        string contentType) =>
        new(
            Guid.NewGuid(),
            type,
            "Title",
            "Body",
            DateTime.UtcNow,
            null,
            contentType,
            Guid.NewGuid(),
            "/poster.jpg");
}
