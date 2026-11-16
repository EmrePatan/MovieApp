using Microsoft.EntityFrameworkCore;
using MovieApp.Application.Abstractions.Identity;
using MovieApp.Application.Abstractions.Social;
using MovieApp.Application.Exceptions;
using MovieApp.Application.Services.Identity;
using MovieApp.Domain.Enums;
using MovieApp.Infrastructure.Persistence;

namespace MovieApp.Infrastructure.Social;

public sealed class SocialProfileService(
    ApplicationDbContext dbContext,
    ICurrentUser currentUser,
    IUserAvatarPresentationService avatarPresentationService) : ISocialProfileService
{
    public async Task<SocialProfileResult> GetProfileAsync(Guid userId, CancellationToken cancellationToken = default)
    {
        var viewerId = currentUser.IsAuthenticated && currentUser.UserId is Guid id ? id : (Guid?)null;
        if (userId == Guid.Empty) throw new NotFoundException("User not found.");

        var user = await dbContext.Users.AsNoTracking()
            .Where(item => item.Id == userId && item.IsActive)
            .Select(item => new { item.Id, item.UserName, item.DisplayName, item.CreatedAt })
            .FirstOrDefaultAsync(cancellationToken);
        if (user is null) throw new NotFoundException("User not found.");

        var blocked = viewerId.HasValue && await dbContext.UserBlocks.AsNoTracking().AnyAsync(
            block => (block.BlockerId == viewerId.Value && block.BlockedUserId == userId)
                || (block.BlockerId == userId && block.BlockedUserId == viewerId.Value), cancellationToken);
        if (blocked) throw new NotFoundException("User not found.");

        var isPrivate = await dbContext.UserPrivacySettings.AsNoTracking()
            .Where(settings => settings.UserId == userId)
            .Select(settings => (bool?)settings.IsPrivate)
            .FirstOrDefaultAsync(cancellationToken) ?? false;
        var isSelf = viewerId == userId;
        var acceptedFollow = viewerId.HasValue && await dbContext.UserFollows.AsNoTracking().AnyAsync(
            follow => follow.FollowerId == viewerId.Value && follow.FollowedUserId == userId
                && follow.Status == UserFollowStatus.Accepted, cancellationToken);
        if (isPrivate && !isSelf && !acceptedFollow) throw new NotFoundException("User not found.");

        var followStatus = "None";
        if (viewerId.HasValue && !isSelf)
        {
            var status = await dbContext.UserFollows.AsNoTracking()
                .Where(follow => follow.FollowerId == viewerId.Value && follow.FollowedUserId == userId)
                .Select(follow => (UserFollowStatus?)follow.Status)
                .FirstOrDefaultAsync(cancellationToken);
            followStatus = status?.ToString() ?? "None";
        }

        var followersCount = await dbContext.UserFollows.AsNoTracking()
            .CountAsync(follow => follow.FollowedUserId == userId && follow.Status == UserFollowStatus.Accepted, cancellationToken);
        var followingCount = await dbContext.UserFollows.AsNoTracking()
            .CountAsync(follow => follow.FollowerId == userId && follow.Status == UserFollowStatus.Accepted, cancellationToken);
        var avatar = await avatarPresentationService.GetForUserAsync(userId, cancellationToken);

        return new SocialProfileResult(user.Id, user.UserName, user.DisplayName, user.CreatedAt,
            avatar.EffectiveAvatarUrl, isPrivate, isSelf ? "Self" : followStatus, followersCount, followingCount);
    }
}
