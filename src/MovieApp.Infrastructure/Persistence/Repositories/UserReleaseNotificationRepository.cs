using Microsoft.EntityFrameworkCore;
using MovieApp.Application.Abstractions.Persistence;
using MovieApp.Application.Models.Notifications;
using MovieApp.Domain.Entities;

namespace MovieApp.Infrastructure.Persistence.Repositories;

public sealed class UserReleaseNotificationRepository(ApplicationDbContext dbContext)
    : IUserReleaseNotificationRepository
{
    public async Task<(IReadOnlyList<NotificationInboxItemResult> Items, int TotalCount)> GetInboxAsync(
        Guid userId,
        int page,
        int pageSize,
        CancellationToken cancellationToken = default)
    {
        var query = dbContext.UserReleaseNotifications
            .AsNoTracking()
            .Where(notification => notification.UserId == userId);

        var totalCount = await query.CountAsync(cancellationToken);

        var items = await query
            .OrderByDescending(notification => notification.CreatedAtUtc)
            .ThenByDescending(notification => notification.Id)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .Select(notification => new NotificationInboxItemResult(
                notification.Id,
                notification.NotificationType,
                notification.Title ?? string.Empty,
                notification.Body,
                notification.CreatedAtUtc,
                notification.ReadAtUtc,
                notification.MovieId.HasValue ? "movie" : "tv",
                notification.MovieId ?? notification.TvShowId!.Value,
                notification.MovieId.HasValue
                    ? notification.Movie!.PosterPath
                    : notification.TvShow!.PosterPath))
            .ToListAsync(cancellationToken);

        return (items, totalCount);
    }

    public async Task<int> GetUnreadCountAsync(Guid userId, CancellationToken cancellationToken = default) =>
        await dbContext.UserReleaseNotifications
            .AsNoTracking()
            .CountAsync(
                notification => notification.UserId == userId && notification.ReadAtUtc == null,
                cancellationToken);

    public async Task<MarkNotificationReadResult?> MarkReadAsync(
        Guid userId,
        Guid notificationId,
        DateTime readAtUtc,
        CancellationToken cancellationToken = default)
    {
        var notification = await dbContext.UserReleaseNotifications
            .FirstOrDefaultAsync(
                item => item.Id == notificationId && item.UserId == userId,
                cancellationToken);

        if (notification is null)
        {
            return null;
        }

        if (notification.ReadAtUtc is null)
        {
            notification.ReadAtUtc = readAtUtc;
            await dbContext.SaveChangesAsync(cancellationToken);
        }

        return new MarkNotificationReadResult(
            notification.Id,
            notification.ReadAtUtc,
            notification.MovieId.HasValue ? "movie" : "tv",
            notification.MovieId ?? notification.TvShowId!.Value);
    }

    public async Task<int> MarkAllReadAsync(
        Guid userId,
        DateTime readAtUtc,
        CancellationToken cancellationToken = default) =>
        await dbContext.UserReleaseNotifications
            .Where(notification => notification.UserId == userId && notification.ReadAtUtc == null)
            .ExecuteUpdateAsync(
                updates => updates.SetProperty(notification => notification.ReadAtUtc, readAtUtc),
                cancellationToken);
}
