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

        return await dbContext.Database.ExecuteInRetriableTransactionAsync(async ct =>
        {
            await SocialPairLock.AcquireAsync(dbContext, followerId, targetUserId, ct);

            if (await IsBlockedEitherWayAsync(followerId, targetUserId, ct))
                throw new NotFoundException("User not found.");

            var existing = await dbContext.UserFollows.FirstOrDefaultAsync(
                follow => follow.FollowerId == followerId && follow.FollowedUserId == targetUserId,
                ct);
            if (existing is not null)
                return ToResult(targetUserId, existing.Status);

            var isPrivate = await dbContext.UserPrivacySettings.AsNoTracking()
                .Where(settings => settings.UserId == targetUserId)
                .Select(settings => (bool?)settings.IsPrivate)
                .FirstOrDefaultAsync(ct) ?? false;

            var now = DateTime.UtcNow;
            var follow = UserFollow.Create(followerId, targetUserId, isPrivate, now);
            var notification = SocialNotification.Create(
                targetUserId, followerId, isPrivate ? "FollowRequest" : "NewFollower",
                null, null, follow.Id, now);
            dbContext.UserFollows.Add(follow);
            dbContext.SocialNotifications.Add(notification);
            await dbContext.SaveChangesAsync(ct);
            return ToResult(targetUserId, follow.Status);
        }, cancellationToken);
    }

    public async Task UnfollowAsync(Guid targetUserId, CancellationToken cancellationToken = default)
    {
        var followerId = GetCurrentUserId();
        await dbContext.Database.ExecuteInRetriableTransactionAsync(async ct =>
        {
            await SocialPairLock.AcquireAsync(dbContext, followerId, targetUserId, ct);
            var follow = await dbContext.UserFollows.FirstOrDefaultAsync(
                item => item.FollowerId == followerId && item.FollowedUserId == targetUserId, ct);
            if (follow is null) return;
            dbContext.UserFollows.Remove(follow);
            await dbContext.SaveChangesAsync(ct);
        }, cancellationToken);
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
        var result = await dbContext.Database.ExecuteInRetriableTransactionAsync<SocialFollowResult?>(
            async ct =>
            {
                await SocialPairLock.AcquireAsync(dbContext, followerUserId, followedUserId, ct);

                var follow = await dbContext.UserFollows.FirstOrDefaultAsync(
                    item => item.FollowerId == followerUserId && item.FollowedUserId == followedUserId,
                    ct);
                if (follow is null || follow.Status != UserFollowStatus.Pending)
                    throw new NotFoundException("Follow request not found.");

                if (await IsBlockedEitherWayAsync(followerUserId, followedUserId, ct))
                {
                    dbContext.UserFollows.Remove(follow);
                    await dbContext.SaveChangesAsync(ct);
                    return null;
                }

                var acceptedAt = DateTime.UtcNow;
                follow.Accept(acceptedAt);
                dbContext.SocialNotifications.Add(SocialNotification.Create(
                    followerUserId, followedUserId, "FollowAccepted", null, null, follow.Id, acceptedAt));
                await dbContext.SaveChangesAsync(ct);
                return ToResult(followerUserId, follow.Status);
            }, cancellationToken);

        return result ?? throw new NotFoundException("Follow request not found.");
    }

    public async Task RejectRequestAsync(Guid followerUserId, CancellationToken cancellationToken = default)
    {
        var followedUserId = GetCurrentUserId();
        await dbContext.Database.ExecuteInRetriableTransactionAsync(async ct =>
        {
            await SocialPairLock.AcquireAsync(dbContext, followerUserId, followedUserId, ct);
            var follow = await dbContext.UserFollows.FirstOrDefaultAsync(
                item => item.FollowerId == followerUserId && item.FollowedUserId == followedUserId
                    && item.Status == UserFollowStatus.Pending, ct);
            if (follow is null) throw new NotFoundException("Follow request not found.");
            dbContext.UserFollows.Remove(follow);
            await dbContext.SaveChangesAsync(ct);
        }, cancellationToken);
    }



    public async Task<IReadOnlyList<SocialBlockedUserResult>> GetBlockedUsersAsync(CancellationToken cancellationToken = default)
    {
        var blockerId = GetCurrentUserId();
        return await dbContext.UserBlocks.AsNoTracking()
            .Where(block => block.BlockerId == blockerId)
            .Join(dbContext.Users.AsNoTracking().Where(user => user.IsActive),
                block => block.BlockedUserId, user => user.Id,
                (block, user) => new SocialBlockedUserResult(user.Id, user.UserName, user.DisplayName, block.CreatedAt))
            .OrderByDescending(item => item.BlockedAt)
            .ToListAsync(cancellationToken);
    }

    public async Task BlockAsync(Guid targetUserId, CancellationToken cancellationToken = default)
    {
        var blockerId = GetCurrentUserId();
        if (targetUserId == Guid.Empty || targetUserId == blockerId)
            throw new ConflictException("You cannot block this user.");

        var targetExists = await dbContext.Users.AsNoTracking()
            .AnyAsync(user => user.Id == targetUserId && user.IsActive, cancellationToken);
        if (!targetExists) throw new NotFoundException("User not found.");

        await dbContext.Database.ExecuteInRetriableTransactionAsync(async ct =>
        {
            await SocialPairLock.AcquireAsync(dbContext, blockerId, targetUserId, ct);

            var alreadyBlocked = await dbContext.UserBlocks.AnyAsync(
                block => block.BlockerId == blockerId && block.BlockedUserId == targetUserId,
                ct);
            if (alreadyBlocked) return;

            dbContext.UserBlocks.Add(UserBlock.Create(blockerId, targetUserId, DateTime.UtcNow));
            var follows = await dbContext.UserFollows.Where(follow =>
                (follow.FollowerId == blockerId && follow.FollowedUserId == targetUserId)
                || (follow.FollowerId == targetUserId && follow.FollowedUserId == blockerId))
                .ToListAsync(ct);
            dbContext.UserFollows.RemoveRange(follows);
            await dbContext.SaveChangesAsync(ct);
        }, cancellationToken);
    }

    public async Task UnblockAsync(Guid targetUserId, CancellationToken cancellationToken = default)
    {
        var blockerId = GetCurrentUserId();
        await dbContext.Database.ExecuteInRetriableTransactionAsync(async ct =>
        {
            await SocialPairLock.AcquireAsync(dbContext, blockerId, targetUserId, ct);
            var block = await dbContext.UserBlocks.FirstOrDefaultAsync(
                item => item.BlockerId == blockerId && item.BlockedUserId == targetUserId, ct);
            if (block is null) return;
            dbContext.UserBlocks.Remove(block);
            await dbContext.SaveChangesAsync(ct);
        }, cancellationToken);
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
