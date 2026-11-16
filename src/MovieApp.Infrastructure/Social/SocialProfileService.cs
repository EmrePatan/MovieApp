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

    public async Task<IReadOnlyList<SocialUserSummaryResult>> SearchUsersAsync(
        string query, int page, int pageSize, CancellationToken cancellationToken = default)
    {
        var viewerId = currentUser.IsAuthenticated && currentUser.UserId is Guid id ? id : (Guid?)null;
        var term = query.Trim();
        if (term.Length < 2) return [];
        var safePage = Math.Max(1, page);
        var safeSize = Math.Clamp(pageSize, 1, 50);
        var users = await dbContext.Users.AsNoTracking()
            .Where(user => user.IsActive && (user.UserName.Contains(term) || user.DisplayName.Contains(term)))
            .Where(user => viewerId == null || user.Id != viewerId.Value)
            .Where(user => viewerId == null || !dbContext.UserBlocks.Any(block =>
                (block.BlockerId == viewerId.Value && block.BlockedUserId == user.Id)
                || (block.BlockerId == user.Id && block.BlockedUserId == viewerId.Value)))
            .OrderBy(user => user.DisplayName).ThenBy(user => user.Id)
            .Skip((safePage - 1) * safeSize).Take(safeSize)
            .Select(user => new { user.Id, user.UserName, user.DisplayName })
            .ToListAsync(cancellationToken);
        return await MapSummariesAsync(users.Select(user => (user.Id, user.UserName, user.DisplayName)).ToList(), viewerId, cancellationToken);
    }

    public async Task<IReadOnlyList<SocialUserSummaryResult>> GetFollowersAsync(
        Guid userId, int page, int pageSize, CancellationToken cancellationToken = default)
    {
        await GetProfileAsync(userId, cancellationToken);
        var safePage = Math.Max(1, page);
        var safeSize = Math.Clamp(pageSize, 1, 50);
        var users = await dbContext.UserFollows.AsNoTracking()
            .Where(follow => follow.FollowedUserId == userId && follow.Status == UserFollowStatus.Accepted)
            .Join(dbContext.Users.AsNoTracking().Where(user => user.IsActive),
                follow => follow.FollowerId, user => user.Id,
                (follow, user) => new { user.Id, user.UserName, user.DisplayName })
            .OrderBy(user => user.DisplayName).ThenBy(user => user.Id)
            .Skip((safePage - 1) * safeSize).Take(safeSize)
            .ToListAsync(cancellationToken);
        var viewerId = currentUser.IsAuthenticated && currentUser.UserId is Guid id ? id : (Guid?)null;
        return await MapSummariesAsync(users.Select(user => (user.Id, user.UserName, user.DisplayName)).ToList(), viewerId, cancellationToken);
    }

    public async Task<IReadOnlyList<SocialUserSummaryResult>> GetFollowingAsync(
        Guid userId, int page, int pageSize, CancellationToken cancellationToken = default)
    {
        await GetProfileAsync(userId, cancellationToken);
        var safePage = Math.Max(1, page);
        var safeSize = Math.Clamp(pageSize, 1, 50);
        var users = await dbContext.UserFollows.AsNoTracking()
            .Where(follow => follow.FollowerId == userId && follow.Status == UserFollowStatus.Accepted)
            .Join(dbContext.Users.AsNoTracking().Where(user => user.IsActive),
                follow => follow.FollowedUserId, user => user.Id,
                (follow, user) => new { user.Id, user.UserName, user.DisplayName })
            .OrderBy(user => user.DisplayName).ThenBy(user => user.Id)
            .Skip((safePage - 1) * safeSize).Take(safeSize)
            .ToListAsync(cancellationToken);
        var viewerId = currentUser.IsAuthenticated && currentUser.UserId is Guid id ? id : (Guid?)null;
        return await MapSummariesAsync(users.Select(user => (user.Id, user.UserName, user.DisplayName)).ToList(), viewerId, cancellationToken);
    }

    private async Task<IReadOnlyList<SocialUserSummaryResult>> MapSummariesAsync(
        IReadOnlyList<(Guid Id, string UserName, string DisplayName)> users,
        Guid? viewerId, CancellationToken cancellationToken)
    {
        var userIds = users.Select(user => user.Id).ToArray();
        var avatars = await avatarPresentationService.GetForUsersAsync(userIds, cancellationToken);
        var statuses = viewerId.HasValue
            ? await dbContext.UserFollows.AsNoTracking()
                .Where(follow => follow.FollowerId == viewerId.Value && userIds.Contains(follow.FollowedUserId))
                .ToDictionaryAsync(follow => follow.FollowedUserId, follow => follow.Status, cancellationToken)
            : new Dictionary<Guid, UserFollowStatus>();
        var result = new List<SocialUserSummaryResult>(users.Count);
        foreach (var user in users)
        {
            var status = statuses.TryGetValue(user.Id, out var followStatus)
                ? followStatus.ToString()
                : "None";
            result.Add(new SocialUserSummaryResult(user.Id, user.UserName, user.DisplayName,
                avatars.TryGetValue(user.Id, out var avatar) ? avatar.EffectiveAvatarUrl : null, status));
        }
        return result;
    }

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
