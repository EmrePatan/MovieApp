using Microsoft.EntityFrameworkCore;
using MovieApp.Application.Abstractions.Identity;
using MovieApp.Application.Abstractions.Social;
using MovieApp.Application.Exceptions;
using MovieApp.Domain.Entities;
using MovieApp.Domain.Enums;
using MovieApp.Infrastructure.Persistence;

namespace MovieApp.Infrastructure.Social;

public sealed class SocialFollowService(ApplicationDbContext dbContext, ICurrentUser currentUser)
    : ISocialFollowService
{
    public async Task<SocialFollowResult> FollowAsync(Guid targetUserId, CancellationToken cancellationToken = default)
    {
        var followerId = GetCurrentUserId();
        if (targetUserId == Guid.Empty || targetUserId == followerId)
            throw new ConflictException("You cannot follow this user.");

        if (!await dbContext.Users.AsNoTracking()
            .AnyAsync(user => user.Id == targetUserId && user.IsActive, cancellationToken))
            throw new NotFoundException("User not found.");

        if (await IsBlockedEitherWayAsync(followerId, targetUserId, cancellationToken))
            throw new NotFoundException("User not found.");

        var existing = await dbContext.UserFollows.FirstOrDefaultAsync(
            follow => follow.FollowerId == followerId && follow.FollowedUserId == targetUserId,
            cancellationToken);
        if (existing is not null)
            return ToResult(targetUserId, existing.Status);

        var isPrivate = await dbContext.UserPrivacySettings.AsNoTracking()
            .Where(settings => settings.UserId == targetUserId)
            .Select(settings => (bool?)settings.IsPrivate)
            .FirstOrDefaultAsync(cancellationToken) ?? false;

        var follow = UserFollow.Create(followerId, targetUserId, isPrivate, DateTime.UtcNow);
        dbContext.UserFollows.Add(follow);
        await dbContext.SaveChangesAsync(cancellationToken);
        return ToResult(targetUserId, follow.Status);
    }

    public async Task UnfollowAsync(Guid targetUserId, CancellationToken cancellationToken = default)
    {
        var followerId = GetCurrentUserId();
        var follow = await dbContext.UserFollows.FirstOrDefaultAsync(
            item => item.FollowerId == followerId && item.FollowedUserId == targetUserId,
            cancellationToken);
        if (follow is null) return;
        dbContext.UserFollows.Remove(follow);
        await dbContext.SaveChangesAsync(cancellationToken);
    }


    public async Task<IReadOnlyList<SocialFollowRequestResult>> GetPendingRequestsAsync(
        int page, int pageSize, CancellationToken cancellationToken = default)
    {
        var followedUserId = GetCurrentUserId();
        var safePage = Math.Max(1, page);
        var safeSize = Math.Clamp(pageSize, 1, 50);
        return await dbContext.UserFollows.AsNoTracking()
            .Where(follow => follow.FollowedUserId == followedUserId && follow.Status == UserFollowStatus.Pending)
            .Join(dbContext.Users.AsNoTracking().Where(user => user.IsActive),
                follow => follow.FollowerId, user => user.Id,
                (follow, user) => new SocialFollowRequestResult(user.Id, user.UserName, user.DisplayName, follow.CreatedAt))
            .OrderByDescending(request => request.RequestedAt).ThenByDescending(request => request.UserId)
            .Skip((safePage - 1) * safeSize).Take(safeSize)
            .ToListAsync(cancellationToken);
    }

    public async Task<SocialFollowResult> AcceptRequestAsync(
        Guid followerUserId, CancellationToken cancellationToken = default)
    {
        var followedUserId = GetCurrentUserId();
        var follow = await dbContext.UserFollows.FirstOrDefaultAsync(
            item => item.FollowerId == followerUserId && item.FollowedUserId == followedUserId,
            cancellationToken);
        if (follow is null || follow.Status != UserFollowStatus.Pending)
            throw new NotFoundException("Follow request not found.");

        if (await IsBlockedEitherWayAsync(followerUserId, followedUserId, cancellationToken))
        {
            dbContext.UserFollows.Remove(follow);
            await dbContext.SaveChangesAsync(cancellationToken);
            throw new NotFoundException("Follow request not found.");
        }

        follow.Accept(DateTime.UtcNow);
        await dbContext.SaveChangesAsync(cancellationToken);
        return ToResult(followerUserId, follow.Status);
    }

    public async Task RejectRequestAsync(Guid followerUserId, CancellationToken cancellationToken = default)
    {
        var followedUserId = GetCurrentUserId();
        var follow = await dbContext.UserFollows.FirstOrDefaultAsync(
            item => item.FollowerId == followerUserId && item.FollowedUserId == followedUserId
                && item.Status == UserFollowStatus.Pending,
            cancellationToken);
        if (follow is null) throw new NotFoundException("Follow request not found.");
        dbContext.UserFollows.Remove(follow);
        await dbContext.SaveChangesAsync(cancellationToken);
    }


    public async Task BlockAsync(Guid targetUserId, CancellationToken cancellationToken = default)
    {
        var blockerId = GetCurrentUserId();
        if (targetUserId == Guid.Empty || targetUserId == blockerId)
            throw new ConflictException("You cannot block this user.");

        var targetExists = await dbContext.Users.AsNoTracking()
            .AnyAsync(user => user.Id == targetUserId && user.IsActive, cancellationToken);
        if (!targetExists) throw new NotFoundException("User not found.");

        var alreadyBlocked = await dbContext.UserBlocks.AnyAsync(
            block => block.BlockerId == blockerId && block.BlockedUserId == targetUserId,
            cancellationToken);
        if (alreadyBlocked) return;

        await using var transaction = await dbContext.Database.BeginTransactionAsync(cancellationToken);
        dbContext.UserBlocks.Add(UserBlock.Create(blockerId, targetUserId, DateTime.UtcNow));
        var follows = await dbContext.UserFollows.Where(follow =>
            (follow.FollowerId == blockerId && follow.FollowedUserId == targetUserId)
            || (follow.FollowerId == targetUserId && follow.FollowedUserId == blockerId))
            .ToListAsync(cancellationToken);
        dbContext.UserFollows.RemoveRange(follows);
        await dbContext.SaveChangesAsync(cancellationToken);
        await transaction.CommitAsync(cancellationToken);
    }

    public async Task UnblockAsync(Guid targetUserId, CancellationToken cancellationToken = default)
    {
        var blockerId = GetCurrentUserId();
        var block = await dbContext.UserBlocks.FirstOrDefaultAsync(
            item => item.BlockerId == blockerId && item.BlockedUserId == targetUserId,
            cancellationToken);
        if (block is null) return;
        dbContext.UserBlocks.Remove(block);
        await dbContext.SaveChangesAsync(cancellationToken);
    }

    private Guid GetCurrentUserId()
    {
        if (!currentUser.IsAuthenticated || currentUser.UserId is not Guid userId || userId == Guid.Empty)
            throw new AuthenticationException("Authentication is required.");
        return userId;
    }

    private Task<bool> IsBlockedEitherWayAsync(Guid first, Guid second, CancellationToken cancellationToken) =>
        dbContext.UserBlocks.AsNoTracking().AnyAsync(
            block => (block.BlockerId == first && block.BlockedUserId == second)
                || (block.BlockerId == second && block.BlockedUserId == first), cancellationToken);

    private static SocialFollowResult ToResult(Guid userId, UserFollowStatus status) =>
        new(userId, status.ToString(), status == UserFollowStatus.Accepted);
}
