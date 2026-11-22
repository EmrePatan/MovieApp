using MovieApp.Domain.Entities;

namespace MovieApp.UnitTests.Domain;

public sealed class SocialNotificationTests
{
    [Fact]
    public void CreateStoresRecipientActorAndTargetReferences()
    {
        var recipientId = Guid.NewGuid();
        var actorId = Guid.NewGuid();
        var activityId = Guid.NewGuid();
        var now = DateTime.UtcNow;

        var notification = SocialNotification.Create(
            recipientId, actorId, "ActivityLike", activityId, null, null, now);

        Assert.Equal(recipientId, notification.RecipientId);
        Assert.Equal(actorId, notification.ActorId);
        Assert.Equal(activityId, notification.ActivityId);
        Assert.Equal(now, notification.CreatedAt);
        Assert.Null(notification.ReadAt);
    }

    [Fact]
    public void UsersCannotNotifyThemselves()
    {
        var userId = Guid.NewGuid();

        Assert.Throws<InvalidOperationException>(() => SocialNotification.Create(
            userId, userId, "ActivityLike", Guid.NewGuid(), null, null, DateTime.UtcNow));
    }

    [Fact]
    public void MarkReadIsIdempotent()
    {
        var notification = SocialNotification.Create(
            Guid.NewGuid(), Guid.NewGuid(), "NewFollower", null, null, Guid.NewGuid(), DateTime.UtcNow);
        var firstReadAt = DateTime.UtcNow.AddMinutes(1);

        notification.MarkRead(firstReadAt);
        notification.MarkRead(firstReadAt.AddMinutes(1));

        Assert.Equal(firstReadAt, notification.ReadAt);
    }
}