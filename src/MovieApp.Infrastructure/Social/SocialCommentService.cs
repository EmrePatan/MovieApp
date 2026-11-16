using Microsoft.EntityFrameworkCore;
using MovieApp.Application.Abstractions.Identity;
using MovieApp.Application.Abstractions.Social;
using MovieApp.Application.Exceptions;
using MovieApp.Domain.Entities;
using MovieApp.Domain.Enums;
using MovieApp.Infrastructure.Persistence;

namespace MovieApp.Infrastructure.Social;

public sealed class SocialCommentService(ApplicationDbContext dbContext, ICurrentUser currentUser) : ISocialCommentService
{
    public async Task<IReadOnlyList<SocialCommentResult>> GetAsync(Guid activityId, Guid? parentCommentId, int page, int pageSize, CancellationToken cancellationToken = default)
    {
        var viewerId = GetUserId();
        await EnsureCanViewActivityAsync(activityId, viewerId, cancellationToken);
        var safePage = Math.Max(1, page);
        var safeSize = Math.Clamp(pageSize, 1, 50);
        return await dbContext.SocialComments.AsNoTracking()
            .Where(comment => comment.ActivityId == activityId && comment.ParentCommentId == parentCommentId)
            .Join(dbContext.Users.AsNoTracking().Where(user => user.IsActive),
                comment => comment.AuthorId, user => user.Id,
                (comment, user) => new { Comment = comment, user.UserName, user.DisplayName })
            .OrderBy(item => item.Comment.CreatedAt).ThenBy(item => item.Comment.Id)
            .Skip((safePage - 1) * safeSize).Take(safeSize)
            .Select(item => new SocialCommentResult(item.Comment.Id, item.Comment.ActivityId, item.Comment.AuthorId,
                item.UserName, item.DisplayName, item.Comment.ParentCommentId,
                item.Comment.IsDeleted ? string.Empty : item.Comment.Content, item.Comment.CreatedAt,
                item.Comment.UpdatedAt, item.Comment.IsDeleted))
            .ToListAsync(cancellationToken);
    }

    public async Task<SocialCommentResult> CreateAsync(Guid activityId, Guid? parentCommentId, string content, CancellationToken cancellationToken = default)
    {
        var userId = GetUserId();
        await EnsureCanViewActivityAsync(activityId, userId, cancellationToken);
        if (parentCommentId.HasValue)
        {
            var parent = await dbContext.SocialComments.AsNoTracking()
                .Where(comment => comment.Id == parentCommentId.Value && comment.ActivityId == activityId)
                .Select(comment => new { comment.AuthorId, comment.IsDeleted })
                .FirstOrDefaultAsync(cancellationToken);
            if (parent is null || parent.IsDeleted) throw new NotFoundException("Parent comment not found.");
            if (await IsBlockedAsync(userId, parent.AuthorId, cancellationToken)) throw new NotFoundException("Parent comment not found.");
        }
        var comment = SocialComment.Create(activityId, userId, parentCommentId, content, DateTime.UtcNow);
        dbContext.SocialComments.Add(comment);
        await dbContext.SaveChangesAsync(cancellationToken);
        var user = await dbContext.Users.AsNoTracking().Where(item => item.Id == userId)
            .Select(item => new { item.UserName, item.DisplayName }).SingleAsync(cancellationToken);
        return new SocialCommentResult(comment.Id, activityId, userId, user.UserName, user.DisplayName,
            parentCommentId, comment.Content, comment.CreatedAt, null, false);
    }

    public async Task<SocialCommentResult> EditAsync(Guid commentId, string content, CancellationToken cancellationToken = default)
    {
        var userId = GetUserId();
        var comment = await dbContext.SocialComments.FirstOrDefaultAsync(item => item.Id == commentId, cancellationToken)
            ?? throw new NotFoundException("Comment not found.");
        comment.Edit(userId, content, DateTime.UtcNow);
        await dbContext.SaveChangesAsync(cancellationToken);
        var user = await dbContext.Users.AsNoTracking().Where(item => item.Id == userId)
            .Select(item => new { item.UserName, item.DisplayName }).SingleAsync(cancellationToken);
        return new SocialCommentResult(comment.Id, comment.ActivityId, userId, user.UserName, user.DisplayName,
            comment.ParentCommentId, comment.Content, comment.CreatedAt, comment.UpdatedAt, comment.IsDeleted);
    }

    public async Task DeleteAsync(Guid commentId, CancellationToken cancellationToken = default)
    {
        var userId = GetUserId();
        var comment = await dbContext.SocialComments.FirstOrDefaultAsync(item => item.Id == commentId, cancellationToken)
            ?? throw new NotFoundException("Comment not found.");
        comment.Delete(userId);
        await dbContext.SaveChangesAsync(cancellationToken);
    }

    private Guid GetUserId()
    {
        if (!currentUser.IsAuthenticated || currentUser.UserId is not Guid id || id == Guid.Empty)
            throw new AuthenticationException("Authentication is required.");
        return id;
    }

    private async Task EnsureCanViewActivityAsync(Guid activityId, Guid viewerId, CancellationToken cancellationToken)
    {
        var activity = await dbContext.SocialActivities.AsNoTracking()
            .Where(item => item.Id == activityId && !item.IsDeleted)
            .Select(item => new { item.ActorId })
            .FirstOrDefaultAsync(cancellationToken);
        if (activity is null) throw new NotFoundException("Activity not found.");
        if (await IsBlockedAsync(viewerId, activity.ActorId, cancellationToken)) throw new NotFoundException("Activity not found.");
        var isPrivate = await dbContext.UserPrivacySettings.AsNoTracking()
            .Where(item => item.UserId == activity.ActorId).Select(item => (bool?)item.IsPrivate)
            .FirstOrDefaultAsync(cancellationToken) ?? false;
        if (isPrivate && activity.ActorId != viewerId &&
            !await dbContext.UserFollows.AnyAsync(item => item.FollowerId == viewerId &&
                item.FollowedUserId == activity.ActorId && item.Status == UserFollowStatus.Accepted, cancellationToken))
            throw new NotFoundException("Activity not found.");
    }

    private Task<bool> IsBlockedAsync(Guid first, Guid second, CancellationToken cancellationToken) =>
        dbContext.UserBlocks.AsNoTracking().AnyAsync(item =>
            (item.BlockerId == first && item.BlockedUserId == second) ||
            (item.BlockerId == second && item.BlockedUserId == first), cancellationToken);
}