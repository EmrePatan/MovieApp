using Microsoft.EntityFrameworkCore;
using MovieApp.Application.Abstractions.Identity;
using MovieApp.Application.Abstractions.Social;
using MovieApp.Application.Exceptions;
using MovieApp.Application.Services.Identity;
using MovieApp.Domain.Entities;
using MovieApp.Domain.Enums;
using MovieApp.Infrastructure.Persistence;

namespace MovieApp.Infrastructure.Social;

public sealed class SocialInteractionService(
    ApplicationDbContext dbContext,
    ICurrentUser currentUser,
    IUserAvatarPresentationService avatarPresentationService) : ISocialInteractionService
{
    public async Task SetActivityLikeAsync(Guid activityId, bool isLiked, CancellationToken cancellationToken = default)
    {
        var viewerId = GetViewerId();
        await EnsureActivityVisibleAsync(activityId, viewerId, cancellationToken);
        if (isLiked)
        {
            await dbContext.Database.ExecuteSqlInterpolatedAsync($"""
                INSERT INTO social_activity_likes ("Id", "ActivityId", "UserId", "CreatedAt")
                VALUES ({Guid.NewGuid()}, {activityId}, {viewerId}, {DateTime.UtcNow})
                ON CONFLICT ("ActivityId", "UserId") DO NOTHING;
                """, cancellationToken);
        }
        else
        {
            await dbContext.SocialActivityLikes
                .Where(like => like.ActivityId == activityId && like.UserId == viewerId)
                .ExecuteDeleteAsync(cancellationToken);
        }
    }

    public async Task<SocialCommentPageResult> GetCommentsAsync(
        Guid activityId, Guid? parentCommentId, int page, int pageSize, CancellationToken cancellationToken = default)
    {
        var viewerId = GetViewerId();
        await EnsureActivityVisibleAsync(activityId, viewerId, cancellationToken);
        if (parentCommentId.HasValue && !await dbContext.SocialComments.AsNoTracking().AnyAsync(
            comment => comment.Id == parentCommentId.Value && comment.ActivityId == activityId, cancellationToken))
            throw new NotFoundException("Comment not found.");

        var safePage = Math.Max(1, page);
        var safeSize = Math.Clamp(pageSize, 1, 50);
        var rows = await (
            from comment in dbContext.SocialComments.AsNoTracking()
            join user in dbContext.Users.AsNoTracking() on comment.AuthorId equals user.Id
            where comment.ActivityId == activityId && comment.ParentCommentId == parentCommentId && user.IsActive
            orderby comment.CreatedAt, comment.Id
            select new
            {
                comment.Id, comment.ActivityId, comment.AuthorId, user.UserName, user.DisplayName,
                comment.ParentCommentId, comment.Content, comment.CreatedAt, comment.UpdatedAt, comment.IsDeleted
            })
            .Skip((safePage - 1) * safeSize).Take(safeSize).ToListAsync(cancellationToken);

        var ids = rows.Select(row => row.Id).ToArray();
        var avatars = await avatarPresentationService.GetForUsersAsync(rows.Select(row => row.AuthorId).Distinct().ToArray(), cancellationToken);
        var likes = await dbContext.SocialCommentLikes.AsNoTracking().Where(like => ids.Contains(like.CommentId))
            .GroupBy(like => like.CommentId).Select(group => new { Id = group.Key, Count = group.Count() })
            .ToDictionaryAsync(item => item.Id, item => item.Count, cancellationToken);
        var replies = await dbContext.SocialComments.AsNoTracking().Where(comment => comment.ParentCommentId != null && ids.Contains(comment.ParentCommentId.Value))
            .GroupBy(comment => comment.ParentCommentId!.Value).Select(group => new { Id = group.Key, Count = group.Count() })
            .ToDictionaryAsync(item => item.Id, item => item.Count, cancellationToken);
        var likedIds = await dbContext.SocialCommentLikes.AsNoTracking()
            .Where(like => like.UserId == viewerId && ids.Contains(like.CommentId))
            .Select(like => like.CommentId).ToHashSetAsync(cancellationToken);

        var items = rows.Select(row => new SocialCommentResult(
            row.Id, row.ActivityId, row.AuthorId, row.UserName, row.DisplayName,
            avatars.TryGetValue(row.AuthorId, out var avatar) ? avatar.EffectiveAvatarUrl : null,
            row.ParentCommentId, row.IsDeleted ? "[deleted]" : row.Content,
            row.CreatedAt, row.UpdatedAt, likes.GetValueOrDefault(row.Id), replies.GetValueOrDefault(row.Id),
            likedIds.Contains(row.Id))).ToList();
        return new SocialCommentPageResult(items, safePage, safeSize);
    }

    public async Task<SocialCommentResult> CreateCommentAsync(
        Guid activityId, Guid? parentCommentId, string content, CancellationToken cancellationToken = default)
    {
        var viewerId = GetViewerId();
        await EnsureActivityVisibleAsync(activityId, viewerId, cancellationToken);
        if (parentCommentId.HasValue && !await dbContext.SocialComments.AsNoTracking().AnyAsync(
            comment => comment.Id == parentCommentId.Value && comment.ActivityId == activityId, cancellationToken))
            throw new NotFoundException("Parent comment not found.");

        var comment = SocialComment.Create(activityId, viewerId, parentCommentId, content, DateTime.UtcNow);
        dbContext.SocialComments.Add(comment);
        await dbContext.SaveChangesAsync(cancellationToken);
        var user = await dbContext.Users.AsNoTracking()
            .Where(item => item.Id == viewerId)
            .Select(item => new { item.UserName, item.DisplayName })
            .FirstAsync(cancellationToken);
        var avatar = await avatarPresentationService.GetForUserAsync(viewerId, cancellationToken);
        return new SocialCommentResult(comment.Id, activityId, viewerId, user.UserName, user.DisplayName,
            avatar.EffectiveAvatarUrl, parentCommentId, comment.Content, comment.CreatedAt, comment.UpdatedAt, 0, 0, false);
    }

    public async Task DeleteCommentAsync(Guid commentId, CancellationToken cancellationToken = default)
    {
        var viewerId = GetViewerId();
        var comment = await dbContext.SocialComments.FirstOrDefaultAsync(item => item.Id == commentId, cancellationToken);
        if (comment is null || comment.AuthorId != viewerId)
            throw new NotFoundException("Comment not found.");
        comment.Delete(viewerId);
        await dbContext.SaveChangesAsync(cancellationToken);
    }

    public async Task SetCommentLikeAsync(Guid commentId, bool isLiked, CancellationToken cancellationToken = default)
    {
        var viewerId = GetViewerId();
        var comment = await dbContext.SocialComments.AsNoTracking()
            .Where(item => item.Id == commentId && !item.IsDeleted)
            .Select(item => new { item.ActivityId })
            .FirstOrDefaultAsync(cancellationToken);
        if (comment is null) throw new NotFoundException("Comment not found.");
        await EnsureActivityVisibleAsync(comment.ActivityId, viewerId, cancellationToken);

        if (isLiked)
        {
            await dbContext.Database.ExecuteSqlInterpolatedAsync($"""
                INSERT INTO social_comment_likes ("Id", "CommentId", "UserId", "CreatedAt")
                VALUES ({Guid.NewGuid()}, {commentId}, {viewerId}, {DateTime.UtcNow})
                ON CONFLICT ("CommentId", "UserId") DO NOTHING;
                """, cancellationToken);
        }
        else
        {
            await dbContext.SocialCommentLikes
                .Where(like => like.CommentId == commentId && like.UserId == viewerId)
                .ExecuteDeleteAsync(cancellationToken);
        }
    }

    public async Task ReportAsync(
        string targetType, Guid targetId, string reason, string? details, CancellationToken cancellationToken = default)
    {
        var viewerId = GetViewerId();
        var normalizedType = targetType.Trim();
        switch (normalizedType)
        {
            case "Activity":
                await EnsureActivityVisibleAsync(targetId, viewerId, cancellationToken);
                break;
            case "Comment":
                var commentActivityId = await dbContext.SocialComments.AsNoTracking()
                    .Where(comment => comment.Id == targetId)
                    .Select(comment => (Guid?)comment.ActivityId).FirstOrDefaultAsync(cancellationToken);
                if (!commentActivityId.HasValue) throw new NotFoundException("Comment not found.");
                await EnsureActivityVisibleAsync(commentActivityId.Value, viewerId, cancellationToken);
                break;
            case "User":
                if (targetId == viewerId) throw new ValidationException("You cannot report yourself.");
                if (!await dbContext.Users.AsNoTracking().AnyAsync(user => user.Id == targetId && user.IsActive, cancellationToken))
                    throw new NotFoundException("User not found.");
                break;
            default:
                throw new ValidationException("Unsupported report target type.");
        }

        dbContext.SocialReports.Add(SocialReport.Create(viewerId, normalizedType, targetId, reason, details, DateTime.UtcNow));
        await dbContext.SaveChangesAsync(cancellationToken);
    }

    private Guid GetViewerId()
    {
        if (!currentUser.IsAuthenticated || currentUser.UserId is not Guid userId || userId == Guid.Empty)
            throw new AuthenticationException("Authentication is required.");
        return userId;
    }

    private async Task EnsureActivityVisibleAsync(Guid activityId, Guid viewerId, CancellationToken cancellationToken)
    {
        var activity = await dbContext.SocialActivities.AsNoTracking()
            .Where(item => item.Id == activityId && !item.IsDeleted)
            .Select(item => new { item.ActorId, item.ActivityType })
            .FirstOrDefaultAsync(cancellationToken);
        if (activity is null) throw new NotFoundException("Activity not found.");
        if (activity.ActorId == viewerId) return;

        var blocked = await dbContext.UserBlocks.AsNoTracking().AnyAsync(block =>
            (block.BlockerId == viewerId && block.BlockedUserId == activity.ActorId)
            || (block.BlockerId == activity.ActorId && block.BlockedUserId == viewerId), cancellationToken);
        if (blocked) throw new NotFoundException("Activity not found.");

        var privacy = await dbContext.UserPrivacySettings.AsNoTracking()
            .Where(settings => settings.UserId == activity.ActorId)
            .Select(settings => new { settings.IsPrivate, settings.ShowWatched, settings.ShowRatings, settings.ShowFavorites, settings.ShowReviews })
            .FirstOrDefaultAsync(cancellationToken);
        if (privacy?.IsPrivate == true && !await dbContext.UserFollows.AsNoTracking().AnyAsync(
            follow => follow.FollowerId == viewerId && follow.FollowedUserId == activity.ActorId
                && follow.Status == UserFollowStatus.Accepted, cancellationToken))
            throw new NotFoundException("Activity not found.");

        var visible = privacy is null || (activity.ActivityType switch
        {
            SocialActivityType.WatchedMovie or SocialActivityType.WatchedEpisode => privacy.ShowWatched,
            SocialActivityType.RatedMovie or SocialActivityType.RatedTvShow => privacy.ShowRatings,
            SocialActivityType.FavoritedMovie or SocialActivityType.FavoritedTvShow => privacy.ShowFavorites,
            _ => privacy.ShowReviews
        });
        if (!visible) throw new NotFoundException("Activity not found.");
    }
}
