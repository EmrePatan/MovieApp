using System.Globalization;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using MovieApp.Application.Abstractions.Identity;
using MovieApp.Application.Abstractions.Persistence;
using MovieApp.Application.Configuration;
using MovieApp.Application.Exceptions;
using MovieApp.Application.Models.Notifications;
using MovieApp.Application.Services.Notifications;
using MovieApp.Domain.Entities;
using MovieApp.Domain.Enums;
using MovieApp.Infrastructure.Persistence;
using MovieApp.Infrastructure.Persistence.Repositories;

namespace MovieApp.UnitTests.Notifications;

public sealed class NotificationRetentionTests
{
    private static readonly Guid UserId = Guid.Parse("bbbbbbbb-bbbb-bbbb-bbbb-bbbbbbbbbbbb");
    private static readonly Guid OtherUserId = Guid.Parse("cccccccc-cccc-cccc-cccc-cccccccccccc");
    private static readonly Guid MovieId = Guid.Parse("dddddddd-dddd-dddd-dddd-dddddddddddd");
    private static readonly DateTime UtcNow = new(2026, 9, 17, 12, 0, 0, DateTimeKind.Utc);

    [Theory]
    [InlineData(null, true)]
    [InlineData("2026-09-10T12:00:01Z", true)]
    [InlineData("2026-09-10T12:00:00Z", false)]
    [InlineData("2026-09-01T00:00:00Z", false)]
    public void IsInboxVisible_UsesReadAtUtcNotCreatedAtUtc(string? readAtUtc, bool expectedVisible)
    {
        var cutoff = NotificationInboxRetention.GetReadExpirationCutoffUtc(UtcNow, 7);
        var readAt = readAtUtc is null
            ? (DateTime?)null
            : DateTime.Parse(readAtUtc, CultureInfo.InvariantCulture, DateTimeStyles.AdjustToUniversal);

        Assert.Equal(expectedVisible, NotificationInboxRetention.IsInboxVisible(readAt, cutoff));
    }

    [Fact]
    public void IsInboxVisible_UnreadOlderThanRetention_RemainsVisible()
    {
        var cutoff = NotificationInboxRetention.GetReadExpirationCutoffUtc(UtcNow, 7);

        Assert.True(NotificationInboxRetention.IsInboxVisible(null, cutoff));
    }

    [Fact]
    public void IsInboxVisible_OldCreatedButNewlyRead_RemainsVisible()
    {
        var cutoff = NotificationInboxRetention.GetReadExpirationCutoffUtc(UtcNow, 7);
        var readAt = UtcNow.AddDays(-1);

        Assert.True(NotificationInboxRetention.IsInboxVisible(readAt, cutoff));
    }

    [Fact]
    public async Task CleanupService_DeletesOnlyEligibleReadNotifications()
    {
        var cutoff = NotificationInboxRetention.GetReadExpirationCutoffUtc(UtcNow, 7);
        var repository = new TrackingNotificationRepository
        {
            DeleteExpiredResult = 2,
        };
        var service = new NotificationInboxCleanupService(
            repository,
            Options.Create(new NotificationRetentionOptions { ReadRetentionDays = 7 }));

        var deleted = await service.CleanupExpiredReadNotificationsAsync();

        Assert.Equal(2, deleted);
        Assert.NotNull(repository.LastExpirationCutoff);
    }

    [Fact]
    public async Task DeleteNotification_OwnNotification_Succeeds()
    {
        var repository = new TrackingNotificationRepository { DeleteResult = true };
        var service = new DeleteNotificationService(new FakeCurrentUser(UserId), repository);

        await service.DeleteAsync(NotificationId);

        Assert.Equal(UserId, repository.LastUserId);
        Assert.Equal(NotificationId, repository.LastNotificationId);
    }

    [Fact]
    public async Task DeleteNotification_ForeignNotification_ReturnsNotFound()
    {
        var repository = new TrackingNotificationRepository { DeleteResult = false };
        var service = new DeleteNotificationService(new FakeCurrentUser(UserId), repository);

        await Assert.ThrowsAsync<NotFoundException>(() => service.DeleteAsync(NotificationId));
    }

    [Fact]
    public async Task Repository_GetInbox_ExcludesExpiredReadNotifications()
    {
        var now = DateTime.UtcNow;
        await using var context = CreateContext();
        var movie = CreateMovie(MovieId);
        context.Movies.Add(movie);
        context.UserReleaseNotifications.AddRange(
            CreateNotification(
                Guid.Parse("11111111-1111-1111-1111-111111111111"),
                UserId,
                readAtUtc: null,
                createdAtUtc: now.AddDays(-30)),
            CreateNotification(
                Guid.Parse("22222222-2222-2222-2222-222222222222"),
                UserId,
                readAtUtc: now.AddDays(-3),
                createdAtUtc: now.AddDays(-30)),
            CreateNotification(
                Guid.Parse("33333333-3333-3333-3333-333333333333"),
                UserId,
                readAtUtc: now.AddDays(-8),
                createdAtUtc: now.AddDays(-1)));
        await context.SaveChangesAsync();

        var repository = CreateRepository(context, readRetentionDays: 7);
        var (items, totalCount) = await repository.GetInboxAsync(UserId, 1, 20);

        Assert.Equal(2, totalCount);
        Assert.Equal(2, items.Count);
        Assert.DoesNotContain(items, item => item.Id == Guid.Parse("33333333-3333-3333-3333-333333333333"));
    }

    private static readonly Guid NotificationId = Guid.Parse("eeeeeeee-eeee-eeee-eeee-eeeeeeeeeeee");

    private static UserReleaseNotificationRepository CreateRepository(
        ApplicationDbContext context,
        int readRetentionDays) =>
        new(context, Options.Create(new NotificationRetentionOptions { ReadRetentionDays = readRetentionDays }));

    private static ApplicationDbContext CreateContext()
    {
        var options = new DbContextOptionsBuilder<ApplicationDbContext>()
            .UseInMemoryDatabase($"notification-retention-{Guid.NewGuid()}")
            .Options;

        return new ApplicationDbContext(options);
    }

    private static Movie CreateMovie(Guid movieId) =>
        new()
        {
            Id = movieId,
            Title = "Test Movie",
            PosterPath = "/poster.jpg",
            CreatedAt = UtcNow,
            UpdatedAt = UtcNow,
        };

    private static UserReleaseNotification CreateNotification(
        Guid id,
        Guid userId,
        DateTime? readAtUtc,
        DateTime createdAtUtc) =>
        new()
        {
            Id = id,
            UserId = userId,
            MovieId = MovieId,
            NotificationType = UserReleaseNotificationType.MovieReleased,
            Status = UserReleaseNotificationStatus.Pending,
            AggregationWindowKey = "window",
            Title = "Title",
            Body = "Body",
            CreatedAtUtc = createdAtUtc,
            ReadAtUtc = readAtUtc,
        };

    private sealed class FakeCurrentUser(Guid? userId) : ICurrentUser
    {
        public bool IsAuthenticated => userId.HasValue;

        public Guid? UserId => userId;
    }

    private sealed class TrackingNotificationRepository : IUserReleaseNotificationRepository
    {
        public Guid? LastUserId { get; private set; }

        public Guid? LastNotificationId { get; private set; }

        public DateTime? LastExpirationCutoff { get; private set; }

        public int DeleteExpiredResult { get; init; }

        public bool DeleteResult { get; init; }

        public Task<(IReadOnlyList<NotificationInboxItemResult> Items, int TotalCount)> GetInboxAsync(
            Guid userId,
            int page,
            int pageSize,
            CancellationToken cancellationToken = default) =>
            Task.FromResult<(IReadOnlyList<NotificationInboxItemResult>, int)>(
                (Array.Empty<NotificationInboxItemResult>(), 0));

        public Task<int> GetUnreadCountAsync(Guid userId, CancellationToken cancellationToken = default) =>
            Task.FromResult(0);

        public Task<MarkNotificationReadResult?> MarkReadAsync(
            Guid userId,
            Guid notificationId,
            DateTime readAtUtc,
            CancellationToken cancellationToken = default) =>
            Task.FromResult<MarkNotificationReadResult?>(null);

        public Task<int> MarkAllReadAsync(
            Guid userId,
            DateTime readAtUtc,
            CancellationToken cancellationToken = default) =>
            Task.FromResult(0);

        public Task<int> DeleteExpiredReadNotificationsAsync(
            DateTime readExpirationCutoffUtc,
            CancellationToken cancellationToken = default)
        {
            LastExpirationCutoff = readExpirationCutoffUtc;
            return Task.FromResult(DeleteExpiredResult);
        }

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
