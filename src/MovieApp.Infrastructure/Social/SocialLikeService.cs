using Microsoft.EntityFrameworkCore;
using MovieApp.Application.Abstractions.Identity;
using MovieApp.Application.Abstractions.Social;
using MovieApp.Application.Exceptions;
using MovieApp.Domain.Entities;
using MovieApp.Domain.Enums;
using MovieApp.Infrastructure.Persistence;

namespace MovieApp.Infrastructure.Social;

public sealed class SocialLikeService(ApplicationDbContext dbContext, ICurrentUser currentUser) : ISocialLikeService
{
    public async Task<SocialLikeResult> LikeActivityAsync(Guid activityId, CancellationToken cancellationToken = default)
    {
        var userId = GetUserId();
        await EnsureCanViewActivityAsync(activityId, userId, cancellationToken);
        await dbContext.Database.ExecuteSqlInterpolatedAsync($"""
            INSERT INTO social_activity_likes ("Id", "ActivityId", "UserId", "CreatedAt")
            VALUES ({Guid.NewGuid()}, {activityId}, {userId}, {DateTime.UtcNow})
            ON CONFLICT ("ActivityId", "UserId") DO NOTHING;
            """, cancellationToken);
        return await GetActivityResultAsync(activityId, userId, cancellationToken);
    }

    public async Task<SocialLikeResult> UnlikeActivityAsync(Guid activityId, CancellationToken cancellationToken = default)
    {
        var userId = GetUserId();
        await EnsureCanViewActivityAsync(activityId, userId, cancellationToken);
        await dbContext.Database.ExecuteSqlInterpolatedAsync($"""
            DELETE FROM social_activity_likes WHERE "ActivityId" = {activityId} AND "UserId" = {userId};
            """, cancellationToken);
        return await GetActivityResultAsync(activityId, userId, cancellationToken);
    }

    public async Task<SocialLikeResult> LikeCommentAsync(Guid commentId, CancellationToken cancellationToken = default)
    {
        var userId = GetUserId();
        var activityId = await GetVisibleCommentActivityIdAsync(commentId, userId, cancellationToken);
        await dbContext.Database.ExecuteSqlInterpolatedAsync($"""
            INSERT INTO social_comment_likes ("Id", "CommentId", "UserId", "CreatedAt")
            VALUES ({Guid.NewGuid()}, {commentId}, {userId}, {DateTime.UtcNow})
            ON CONFLICT ("CommentId", "UserId") DO NOTHING;
            """, cancellationToken);
        return await GetCommentResultAsync(commentId, userId, cancellationToken);
    }

    public async Task<SocialLikeResult> UnlikeCommentAsync(Guid commentId, CancellationToken cancellationToken = default)
    {
        var userId = GetUserId();
        await GetVisibleCommentActivityIdAsync(commentId, userId, cancellationToken);
        await dbContext.Database.ExecuteSqlInterpolatedAsync($"""
            DELETE FROM social_comment_likes WHERE "CommentId" = {commentId} AND "UserId" = {userId};
            """, cancellationToken);
        return await GetCommentResultAsync(commentId, userId, cancellationToken);
    }

    private async Task<SocialLikeResult> GetActivityResultAsync(Guid activityId, Guid userId, CancellationToken cancellationToken)
    {
        var exists = await dbContext.SocialActivities.AsNoTracking().AnyAsync(item => item.Id == activityId && !item.IsDeleted, cancellationToken);
        if (!exists) throw new NotFoundException("Activity not found.");
        var liked = await dbContext.SocialActivityLikes.AsNoTracking().AnyAsync(item => item.ActivityId == activityId && item.UserId == userId, cancellationToken);
        var count = await dbContext.SocialActivityLikes.AsNoTracking().CountAsync(item => item.ActivityId == activityId, cancellationToken);
        return new SocialLikeResult(liked, count);
    }

    private async Task<SocialLikeResult> GetCommentResultAsync(Guid commentId, Guid userId, CancellationToken cancellationToken)
    {
        var exists = await dbContext.SocialComments.AsNoTracking().AnyAsync(item => item.Id == commentId && !item.IsDeleted, cancellationToken);
        if (!exists) throw new NotFoundException("Comment not found.");
        var liked = await dbContext.SocialCommentLikes.AsNoTracking().AnyAsync(item => item.CommentId == commentId && item.UserId == userId, cancellationToken);
        var count = await dbContext.SocialCommentLikes.AsNoTracking().CountAsync(item => item.CommentId == commentId, cancellationToken);
        return new SocialLikeResult(liked, count);
    }

    private async Task<Guid> GetVisibleCommentActivityIdAsync(Guid commentId, Guid userId, CancellationToken cancellationToken)
    {
        var activityId = await dbContext.SocialComments.AsNoTracking()
            .Where(item => item.Id == commentId && !item.IsDeleted)
            .Select(item => (Guid?)item.ActivityId).FirstOrDefaultAsync(cancellationToken);
        if (!activityId.HasValue) throw new NotFoundException("Comment not found.");
        await EnsureCanViewActivityAsync(activityId.Value, userId, cancellationToken);
        return activityId.Value;
    }

    private async Task EnsureCanViewActivityAsync(Guid activityId, Guid viewerId, CancellationToken cancellationToken)
    {
        var actorId = await dbContext.SocialActivities.AsNoTracking()
            .Where(item => item.Id == activityId && !item.IsDeleted)
            .Select(item => (Guid?)item.ActorId).FirstOrDefaultAsync(cancellationToken);
        if (!actorId.HasValue) throw new NotFoundException("Activity not found.");
        var blocked = await dbContext.UserBlocks.AsNoTracking().AnyAsync(item =>
            (item.BlockerId == viewerId && item.BlockedUserId == actorId.Value)
            || (item.BlockerId == actorId.Value && item.BlockedUserId == viewerId), cancellationToken);
        if (blocked) throw new NotFoundException("Activity not found.");
        var isPrivate = await dbContext.UserPrivacySettings.AsNoTracking()
            .Where(item => item.UserId == actorId.Value).Select(item => (bool?)item.IsPrivate)
            .FirstOrDefaultAsync(cancellationToken) ?? false;
        if (isPrivate && actorId.Value != viewerId &&
            !await dbContext.UserFollows.AsNoTracking().AnyAsync(item => item.FollowerId == viewerId
                && item.FollowedUserId == actorId.Value && item.Status == UserFollowStatus.Accepted, cancellationToken))
            throw new NotFoundException("Activity not found.");
    }

    private Guid GetUserId()
    {
        if (!currentUser.IsAuthenticated || currentUser.UserId is not Guid id || id == Guid.Empty)
            throw new AuthenticationException("Authentication is required.");
        return id;
    }
}
