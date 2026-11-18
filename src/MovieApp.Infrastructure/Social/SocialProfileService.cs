using Microsoft.EntityFrameworkCore;
using MovieApp.Application.Abstractions.Identity;
using MovieApp.Application.Abstractions.Persistence;
using MovieApp.Application.Abstractions.Social;
using MovieApp.Application.Exceptions;
using MovieApp.Application.Services.Identity;
using MovieApp.Domain.Enums;
using MovieApp.Infrastructure.Persistence;

namespace MovieApp.Infrastructure.Social;

public sealed class SocialProfileService(
    ApplicationDbContext dbContext,
    ICurrentUser currentUser,
    IUserAvatarPresentationService avatarPresentationService,
    IUserStatisticsRepository userStatisticsRepository) : ISocialProfileService
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
        await EnsureCanViewRelationshipListAsync(userId, cancellationToken);
        var safePage = Math.Max(1, page);
        var safeSize = Math.Clamp(pageSize, 1, 50);
        var viewerId = currentUser.IsAuthenticated && currentUser.UserId is Guid id ? id : (Guid?)null;
        var users = await dbContext.UserFollows.AsNoTracking()
            .Where(follow => follow.FollowedUserId == userId && follow.Status == UserFollowStatus.Accepted)
            .Join(dbContext.Users.AsNoTracking().Where(user => user.IsActive),
                follow => follow.FollowerId, user => user.Id,
                (follow, user) => new { user.Id, user.UserName, user.DisplayName })
            .Where(user => !dbContext.UserBlocks.Any(block =>
                (block.BlockerId == userId && block.BlockedUserId == user.Id)
                || (block.BlockerId == user.Id && block.BlockedUserId == userId)))
            .Where(user => !viewerId.HasValue || !dbContext.UserBlocks.Any(block =>
                (block.BlockerId == viewerId.Value && block.BlockedUserId == user.Id)
                || (block.BlockerId == user.Id && block.BlockedUserId == viewerId.Value)))
            .OrderBy(user => user.DisplayName).ThenBy(user => user.Id)
            .Skip((safePage - 1) * safeSize).Take(safeSize)
            .ToListAsync(cancellationToken);
        return await MapSummariesAsync(users.Select(user => (user.Id, user.UserName, user.DisplayName)).ToList(), viewerId, cancellationToken);
    }

    public async Task<IReadOnlyList<SocialUserSummaryResult>> GetFollowingAsync(
        Guid userId, int page, int pageSize, CancellationToken cancellationToken = default)
    {
        await GetProfileAsync(userId, cancellationToken);
        await EnsureCanViewRelationshipListAsync(userId, cancellationToken);
        var safePage = Math.Max(1, page);
        var safeSize = Math.Clamp(pageSize, 1, 50);
        var viewerId = currentUser.IsAuthenticated && currentUser.UserId is Guid id ? id : (Guid?)null;
        var users = await dbContext.UserFollows.AsNoTracking()
            .Where(follow => follow.FollowerId == userId && follow.Status == UserFollowStatus.Accepted)
            .Join(dbContext.Users.AsNoTracking().Where(user => user.IsActive),
                follow => follow.FollowedUserId, user => user.Id,
                (follow, user) => new { user.Id, user.UserName, user.DisplayName })
            .Where(user => !dbContext.UserBlocks.Any(block =>
                (block.BlockerId == userId && block.BlockedUserId == user.Id)
                || (block.BlockerId == user.Id && block.BlockedUserId == userId)))
            .Where(user => !viewerId.HasValue || !dbContext.UserBlocks.Any(block =>
                (block.BlockerId == viewerId.Value && block.BlockedUserId == user.Id)
                || (block.BlockerId == user.Id && block.BlockedUserId == viewerId.Value)))
            .OrderBy(user => user.DisplayName).ThenBy(user => user.Id)
            .Skip((safePage - 1) * safeSize).Take(safeSize)
            .ToListAsync(cancellationToken);
        return await MapSummariesAsync(users.Select(user => (user.Id, user.UserName, user.DisplayName)).ToList(), viewerId, cancellationToken);
    }

    public async Task<SocialProfileStatisticsResult> GetStatisticsAsync(Guid userId, CancellationToken cancellationToken = default)
    {
        var profile = await GetProfileAsync(userId, cancellationToken);
        if (profile.IsPrivate && profile.FollowStatus is not ("Accepted" or "Self"))
            throw new NotFoundException("User not found.");

        var privacy = await dbContext.UserPrivacySettings.AsNoTracking()
            .Where(settings => settings.UserId == userId)
            .Select(settings => new
            {
                settings.ShowWatched,
                settings.ShowRatings,
                settings.ShowFavorites,
                settings.ShowReviews
            })
            .FirstOrDefaultAsync(cancellationToken);

        var showWatched = privacy?.ShowWatched ?? true;
        var showRatings = privacy?.ShowRatings ?? true;
        var showFavorites = privacy?.ShowFavorites ?? true;
        var showReviews = privacy?.ShowReviews ?? true;
        var statistics = await userStatisticsRepository.GetStatisticsAsync(userId, cancellationToken: cancellationToken);

        var milestones = statistics.Milestones
            .Where(milestone =>
                (showWatched && (milestone.Id.StartsWith("first-movie", StringComparison.Ordinal)
                    || milestone.Id.StartsWith("movies-", StringComparison.Ordinal)
                    || milestone.Id.StartsWith("episodes-", StringComparison.Ordinal)
                    || milestone.Id == "first-show-completed"))
                || (showRatings && milestone.Id.StartsWith("ratings-", StringComparison.Ordinal)))
            .Select(milestone => new SocialProfileMilestoneResult(
                milestone.Id, milestone.Title, milestone.Description, milestone.AchievedAt))
            .ToList();

        return new SocialProfileStatisticsResult(
            showWatched ? statistics.Summary.MoviesWatched : 0,
            showWatched ? statistics.Summary.EpisodesWatched : 0,
            showRatings ? statistics.Summary.RatingsCount : 0,
            showReviews ? statistics.Summary.ReviewsCount : 0,
            showFavorites ? statistics.Summary.FavoritesCount : 0,
            milestones);
    }

    private async Task EnsureCanViewRelationshipListAsync(Guid userId, CancellationToken cancellationToken)
    {
        var viewerId = currentUser.IsAuthenticated && currentUser.UserId is Guid id ? id : (Guid?)null;
        if (viewerId == userId) return;
        var isPrivate = await dbContext.UserPrivacySettings.AsNoTracking()
            .Where(settings => settings.UserId == userId)
            .Select(settings => (bool?)settings.IsPrivate)
            .FirstOrDefaultAsync(cancellationToken) ?? false;
        if (!isPrivate) return;
        var accepted = viewerId.HasValue && await dbContext.UserFollows.AsNoTracking().AnyAsync(
            follow => follow.FollowerId == viewerId.Value && follow.FollowedUserId == userId
                && follow.Status == UserFollowStatus.Accepted, cancellationToken);
        if (!accepted) throw new NotFoundException("User not found.");
    }

    private async Task<IReadOnlyList<SocialUserSummaryResult>> MapSummariesAsync(
        List<(Guid Id, string UserName, string DisplayName)> users,
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
        var followStatus = "None";
        if (viewerId.HasValue && !isSelf)
        {
            var status = await dbContext.UserFollows.AsNoTracking()
                .Where(follow => follow.FollowerId == viewerId.Value && follow.FollowedUserId == userId)
                .Select(follow => (UserFollowStatus?)follow.Status)
                .FirstOrDefaultAsync(cancellationToken);
            followStatus = status?.ToString() ?? "None";
        }

        var followersCount = await (
            from follow in dbContext.UserFollows.AsNoTracking()
            join follower in dbContext.Users.AsNoTracking() on follow.FollowerId equals follower.Id
            where follow.FollowedUserId == userId && follow.Status == UserFollowStatus.Accepted && follower.IsActive
                && !dbContext.UserBlocks.Any(block =>
                    (block.BlockerId == userId && block.BlockedUserId == follower.Id)
                    || (block.BlockerId == follower.Id && block.BlockedUserId == userId))
            select follow.Id
        ).CountAsync(cancellationToken);
        var followingCount = await (
            from follow in dbContext.UserFollows.AsNoTracking()
            join followedUser in dbContext.Users.AsNoTracking() on follow.FollowedUserId equals followedUser.Id
            where follow.FollowerId == userId && follow.Status == UserFollowStatus.Accepted && followedUser.IsActive
                && !dbContext.UserBlocks.Any(block =>
                    (block.BlockerId == userId && block.BlockedUserId == followedUser.Id)
                    || (block.BlockerId == followedUser.Id && block.BlockedUserId == userId))
            select follow.Id
        ).CountAsync(cancellationToken);
        var avatar = await avatarPresentationService.GetForUserAsync(userId, cancellationToken);

        return new SocialProfileResult(user.Id, user.UserName, user.DisplayName, user.CreatedAt,
            avatar.EffectiveAvatarUrl, isPrivate, isSelf ? "Self" : followStatus, followersCount, followingCount);
    }
}
