using MovieApp.Domain.Entities;
using MovieApp.Domain.Enums;

namespace MovieApp.UnitTests.Domain;

public sealed class SocialGraphEntityTests
{
    [Fact]
    public void CreateFollowForPublicProfileAcceptsImmediately()
    {
        var now = DateTime.UtcNow;

        var follow = UserFollow.Create(Guid.NewGuid(), Guid.NewGuid(), false, now);

        Assert.Equal(UserFollowStatus.Accepted, follow.Status);
        Assert.Equal(now, follow.CreatedAt);
        Assert.Equal(now, follow.RespondedAt);
    }

    [Fact]
    public void CreateFollowForPrivateProfileCreatesPendingRequest()
    {
        var follow = UserFollow.Create(Guid.NewGuid(), Guid.NewGuid(), true, DateTime.UtcNow);

        Assert.Equal(UserFollowStatus.Pending, follow.Status);
        Assert.Null(follow.RespondedAt);
    }

    [Fact]
    public void FollowCannotBeCreatedForSelf()
    {
        var userId = Guid.NewGuid();

        Assert.Throws<InvalidOperationException>(() =>
            UserFollow.Create(userId, userId, false, DateTime.UtcNow));
    }

    [Fact]
    public void AcceptPendingFollowRecordsResponseTime()
    {
        var follow = UserFollow.Create(Guid.NewGuid(), Guid.NewGuid(), true, DateTime.UtcNow);
        var acceptedAt = DateTime.UtcNow.AddMinutes(1);

        follow.Accept(acceptedAt);

        Assert.Equal(UserFollowStatus.Accepted, follow.Status);
        Assert.Equal(acceptedAt, follow.RespondedAt);
    }

    [Fact]
    public void AcceptingAlreadyAcceptedFollowIsRejected()
    {
        var follow = UserFollow.Create(Guid.NewGuid(), Guid.NewGuid(), false, DateTime.UtcNow);

        Assert.Throws<InvalidOperationException>(() => follow.Accept(DateTime.UtcNow));
    }

    [Fact]
    public void DefaultPrivacySettingsArePublicAndShareActivityCategories()
    {
        var settings = UserPrivacySettings.CreateDefault(Guid.NewGuid(), DateTime.UtcNow);

        Assert.False(settings.IsPrivate);
        Assert.True(settings.ShowWatched);
        Assert.True(settings.ShowRatings);
        Assert.True(settings.ShowFavorites);
        Assert.True(settings.ShowReviews);
    }

    [Fact]
    public void PrivacySettingsCanBeUpdated()
    {
        var settings = UserPrivacySettings.CreateDefault(Guid.NewGuid(), DateTime.UtcNow);
        var updatedAt = DateTime.UtcNow.AddMinutes(1);

        settings.Update(true, false, true, false, true, updatedAt);

        Assert.True(settings.IsPrivate);
        Assert.False(settings.ShowWatched);
        Assert.True(settings.ShowRatings);
        Assert.False(settings.ShowFavorites);
        Assert.True(settings.ShowReviews);
        Assert.Equal(updatedAt, settings.UpdatedAt);
    }

    [Fact]
    public void UserCannotBlockSelf()
    {
        var userId = Guid.NewGuid();

        Assert.Throws<InvalidOperationException>(() =>
            UserBlock.Create(userId, userId, DateTime.UtcNow));
    }
}
