using MovieApp.Application.Abstractions.Identity;
using MovieApp.Application.Abstractions.Persistence;
using MovieApp.Application.Exceptions;
using MovieApp.Application.Models.Notifications;
using MovieApp.Application.Services.Notifications;
using MovieApp.Domain.Enums;
using Microsoft.Extensions.Logging.Abstractions;

namespace MovieApp.UnitTests.Notifications;

public sealed class NotificationServiceTests
{
    private static readonly Guid UserId = Guid.Parse("bbbbbbbb-bbbb-bbbb-bbbb-bbbbbbbbbbbb");
    private static readonly Guid OtherUserId = Guid.Parse("cccccccc-cccc-cccc-cccc-cccccccccccc");
    private static readonly Guid MovieId = Guid.Parse("dddddddd-dddd-dddd-dddd-dddddddddddd");
    private static readonly Guid NotificationId = Guid.Parse("eeeeeeee-eeee-eeee-eeee-eeeeeeeeeeee");

    [Fact]
    public async Task GetNotifications_ReturnsOnlyCurrentUserItems()
    {
        var repository = new FakeNotificationRepository
        {
            InboxItems =
            [
                CreateItem(NotificationId, "movie", MovieId),
            ],
            TotalCount = 1,
        };
        var service = new GetNotificationsService(new FakeCurrentUser(UserId), repository);

        var result = await service.GetAsync(1, 20);

        Assert.Equal(UserId, repository.LastUserId);
        Assert.Single(result.Items);
        Assert.Equal("movie", result.Items[0].ContentType);
    }

    [Fact]
    public async Task GetNotifications_RejectsInvalidPagination()
    {
        var service = new GetNotificationsService(
            new FakeCurrentUser(UserId),
            new FakeNotificationRepository());

        await Assert.ThrowsAsync<ValidationException>(() => service.GetAsync(0, 20));
    }

    [Fact]
    public async Task GetUnreadCount_ReturnsRepositoryCount()
    {
        var repository = new FakeNotificationRepository { UnreadCount = 3 };
        var service = new GetUnreadNotificationCountService(
            new FakeCurrentUser(UserId),
            repository,
            NullLogger<GetUnreadNotificationCountService>.Instance);

        var count = await service.GetAsync();

        Assert.Equal(3, count);
        Assert.Equal(UserId, repository.LastUserId);
    }

    [Fact]
    public async Task MarkRead_SetsReadAtUtc()
    {
        var readAt = new DateTime(2026, 9, 15, 8, 0, 0, DateTimeKind.Utc);
        var repository = new FakeNotificationRepository
        {
            MarkReadResult = new MarkNotificationReadResult(
                NotificationId,
                readAt,
                "movie",
                MovieId),
        };
        var service = new MarkNotificationReadService(new FakeCurrentUser(UserId), repository);

        var result = await service.MarkAsync(NotificationId);

        Assert.Equal(NotificationId, result.Id);
        Assert.Equal("movie", result.ContentType);
        Assert.Equal(MovieId, result.ContentId);
        Assert.Equal(UserId, repository.LastUserId);
        Assert.Equal(NotificationId, repository.LastNotificationId);
    }

    [Fact]
    public async Task MarkRead_ForeignNotification_ReturnsNotFound()
    {
        var repository = new FakeNotificationRepository { MarkReadResult = null };
        var service = new MarkNotificationReadService(new FakeCurrentUser(UserId), repository);

        await Assert.ThrowsAsync<NotFoundException>(() => service.MarkAsync(NotificationId));
    }

    [Fact]
    public async Task MarkAllRead_ReturnsAffectedCount()
    {
        var repository = new FakeNotificationRepository { MarkAllAffectedCount = 4 };
        var service = new MarkAllNotificationsReadService(new FakeCurrentUser(UserId), repository);

        var result = await service.MarkAllAsync();

        Assert.Equal(4, result.AffectedCount);
        Assert.Equal(UserId, repository.LastUserId);
    }

    [Fact]
    public async Task GetNotifications_RequiresAuthentication()
    {
        var service = new GetNotificationsService(
            new FakeCurrentUser(null),
            new FakeNotificationRepository());

        await Assert.ThrowsAsync<AuthenticationException>(() => service.GetAsync(1, 20));
    }

    [Fact]
    public async Task DeleteNotification_OwnNotification_Succeeds()
    {
        var repository = new FakeNotificationRepository { DeleteResult = true };
        var service = new DeleteNotificationService(new FakeCurrentUser(UserId), repository);

        await service.DeleteAsync(NotificationId);

        Assert.Equal(UserId, repository.LastUserId);
        Assert.Equal(NotificationId, repository.LastNotificationId);
    }

    [Fact]
    public async Task DeleteNotification_ForeignNotification_ReturnsNotFound()
    {
        var repository = new FakeNotificationRepository { DeleteResult = false };
        var service = new DeleteNotificationService(new FakeCurrentUser(UserId), repository);

        await Assert.ThrowsAsync<NotFoundException>(() => service.DeleteAsync(NotificationId));
    }

    private static NotificationInboxItemResult CreateItem(
        Guid id,
        string contentType,
        Guid contentId) =>
        new(
            id,
            UserReleaseNotificationType.MovieReleased,
            "Inception",
            "Now available",
            new DateTime(2026, 9, 15, 12, 0, 0, DateTimeKind.Utc),
            null,
            contentType,
            contentId,
            "/poster.jpg");

    private sealed class FakeCurrentUser(Guid? userId) : ICurrentUser
    {
        public bool IsAuthenticated => userId.HasValue;

        public Guid? UserId => userId;
    }

    private sealed class FakeNotificationRepository : IUserReleaseNotificationRepository
    {
        public Guid? LastUserId { get; private set; }

        public Guid? LastNotificationId { get; private set; }

        public IReadOnlyList<NotificationInboxItemResult> InboxItems { get; init; } = [];

        public int TotalCount { get; init; }

        public int UnreadCount { get; init; }

        public MarkNotificationReadResult? MarkReadResult { get; init; }

        public int MarkAllAffectedCount { get; init; }

        public bool DeleteResult { get; init; }

        public Task<(IReadOnlyList<NotificationInboxItemResult> Items, int TotalCount)> GetInboxAsync(
            Guid userId,
            int page,
            int pageSize,
            CancellationToken cancellationToken = default)
        {
            LastUserId = userId;
            return Task.FromResult((InboxItems, TotalCount));
        }

        public Task<int> GetUnreadCountAsync(Guid userId, CancellationToken cancellationToken = default)
        {
            LastUserId = userId;
            return Task.FromResult(UnreadCount);
        }

        public Task<MarkNotificationReadResult?> MarkReadAsync(
            Guid userId,
            Guid notificationId,
            DateTime readAtUtc,
            CancellationToken cancellationToken = default)
        {
            LastUserId = userId;
            LastNotificationId = notificationId;
            return Task.FromResult(MarkReadResult);
        }

        public Task<int> MarkAllReadAsync(
            Guid userId,
            DateTime readAtUtc,
            CancellationToken cancellationToken = default)
        {
            LastUserId = userId;
            return Task.FromResult(MarkAllAffectedCount);
        }

        public Task<int> DeleteExpiredReadNotificationsAsync(
            DateTime readExpirationCutoffUtc,
            CancellationToken cancellationToken = default) =>
            Task.FromResult(0);

        public Task<bool> DeleteAsync(
            Guid userId,
            Guid notificationId,
            CancellationToken cancellationToken = default)
        {
            LastUserId = userId;
            LastNotificationId = notificationId;
            return Task.FromResult(DeleteResult);
        }
    }
}
