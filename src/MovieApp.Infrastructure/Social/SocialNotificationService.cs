using Microsoft.EntityFrameworkCore;
using MovieApp.Application.Abstractions.Identity;
using MovieApp.Application.Abstractions.Social;
using MovieApp.Application.Exceptions;
using MovieApp.Application.Services.Identity;
using MovieApp.Infrastructure.Persistence;

namespace MovieApp.Infrastructure.Social;

public sealed class SocialNotificationService(
    ApplicationDbContext dbContext,
    ICurrentUser currentUser,
    IUserAvatarPresentationService avatarPresentationService) : ISocialNotificationService
{
    public async Task<SocialNotificationPage> GetAsync(int page, int pageSize, CancellationToken cancellationToken = default)
    {
        var userId = GetUserId();
        var safePage = Math.Max(1, page);
        var safeSize = Math.Clamp(pageSize, 1, 50);
        var query = dbContext.SocialNotifications.AsNoTracking().Where(item => item.RecipientId == userId);
        var unreadCount = await query.CountAsync(item => item.ReadAt == null, cancellationToken);
        var rows = await (
            from notification in query
            join actor in dbContext.Users.AsNoTracking() on notification.ActorId equals actor.Id
            where actor.IsActive
                && !dbContext.UserBlocks.Any(block =>
                    (block.BlockerId == userId && block.BlockedUserId == actor.Id)
                    || (block.BlockerId == actor.Id && block.BlockedUserId == userId))
            orderby notification.CreatedAt descending, notification.Id descending
            select new
            {
                notification.Id, notification.NotificationType, ActorId = actor.Id, actor.UserName,
                actor.DisplayName, notification.ActivityId, notification.CommentId, notification.FollowId,
                notification.CreatedAt, notification.ReadAt
            })
            .Skip((safePage - 1) * safeSize).Take(safeSize).ToListAsync(cancellationToken);
        var avatars = rows.Count == 0
            ? new Dictionary<Guid, UserAvatarPresentation>()
            : await avatarPresentationService.GetForUsersAsync(
                rows.Select(row => row.ActorId).Distinct().ToArray(), cancellationToken);
        var items = rows.Select(row => new SocialNotificationItem(
            row.Id, row.NotificationType, row.ActorId, row.UserName, row.DisplayName,
            avatars.TryGetValue(row.ActorId, out var avatar) ? avatar.EffectiveAvatarUrl : null,
            row.ActivityId, row.CommentId, row.FollowId, row.CreatedAt, row.ReadAt.HasValue)).ToList();
        return new SocialNotificationPage(items, safePage, safeSize, unreadCount);
    }

    public async Task<int> GetUnreadCountAsync(CancellationToken cancellationToken = default)
    {
        var userId = GetUserId();
        return await dbContext.SocialNotifications.AsNoTracking()
            .CountAsync(item => item.RecipientId == userId && item.ReadAt == null, cancellationToken);
    }

    public async Task MarkReadAsync(Guid notificationId, CancellationToken cancellationToken = default)
    {
        var userId = GetUserId();
        var notification = await dbContext.SocialNotifications.FirstOrDefaultAsync(
            item => item.Id == notificationId && item.RecipientId == userId, cancellationToken)
            ?? throw new NotFoundException("Notification not found.");
        notification.MarkRead(DateTime.UtcNow);
        await dbContext.SaveChangesAsync(cancellationToken);
    }

    public async Task<int> MarkAllReadAsync(CancellationToken cancellationToken = default)
    {
        var userId = GetUserId();
        var now = DateTime.UtcNow;
        return await dbContext.SocialNotifications
            .Where(item => item.RecipientId == userId && item.ReadAt == null)
            .ExecuteUpdateAsync(setters => setters.SetProperty(item => item.ReadAt, now), cancellationToken);
    }

    private Guid GetUserId()
    {
        if (!currentUser.IsAuthenticated || currentUser.UserId is not Guid userId || userId == Guid.Empty)
            throw new AuthenticationException("Authentication is required.");
        return userId;
    }
}