using Microsoft.EntityFrameworkCore;
using MovieApp.Application.Abstractions.Social;
using MovieApp.Domain.Enums;
using MovieApp.Infrastructure.Persistence;

namespace MovieApp.Infrastructure.Social;

public sealed class SocialReviewEngagementLookup(ApplicationDbContext dbContext) : ISocialReviewEngagementLookup
{
    private static readonly SocialActivityType[] ReviewActivityTypes =
    [
        SocialActivityType.ReviewedMovie,
        SocialActivityType.ReviewedTvShow,
        SocialActivityType.ReviewedEpisode
    ];

    public async Task<IReadOnlyDictionary<Guid, SocialReviewEngagement>> GetForReviewIdsAsync(
        IReadOnlyList<Guid> reviewIds,
        Guid? viewerUserId,
        CancellationToken cancellationToken = default)
    {
        if (reviewIds.Count == 0) return new Dictionary<Guid, SocialReviewEngagement>();

        var ids = reviewIds.Where(id => id != Guid.Empty).Distinct().ToArray();
        if (ids.Length == 0) return new Dictionary<Guid, SocialReviewEngagement>();

        var activities = await dbContext.SocialActivities.AsNoTracking()
            .Where(activity => !activity.IsDeleted
                && ReviewActivityTypes.Contains(activity.ActivityType)
                && ids.Contains(activity.SourceEntityId))
            .Select(activity => new { activity.Id, activity.SourceEntityId })
            .ToListAsync(cancellationToken);

        if (activities.Count == 0) return new Dictionary<Guid, SocialReviewEngagement>();

        // One active activity per review source; prefer newest if duplicates exist.
        var activityByReview = activities
            .GroupBy(row => row.SourceEntityId)
            .ToDictionary(group => group.Key, group => group.OrderByDescending(row => row.Id).First());

        var activityIds = activityByReview.Values.Select(row => row.Id).ToArray();

        var likes = await (
            from like in dbContext.SocialActivityLikes.AsNoTracking()
            join user in dbContext.Users.AsNoTracking() on like.UserId equals user.Id
            where activityIds.Contains(like.ActivityId) && user.IsActive
            group like by like.ActivityId into grouped
            select new { Id = grouped.Key, Count = grouped.Count() })
            .ToDictionaryAsync(item => item.Id, item => item.Count, cancellationToken);

        var comments = await (
            from comment in dbContext.SocialComments.AsNoTracking()
            join author in dbContext.Users.AsNoTracking() on comment.AuthorId equals author.Id
            where activityIds.Contains(comment.ActivityId)
                && comment.ParentCommentId == null
                && !comment.IsDeleted
                && author.IsActive
            group comment by comment.ActivityId into grouped
            select new { Id = grouped.Key, Count = grouped.Count() })
            .ToDictionaryAsync(item => item.Id, item => item.Count, cancellationToken);

        HashSet<Guid> likedIds = [];
        if (viewerUserId is Guid viewerId && viewerId != Guid.Empty)
        {
            likedIds = await dbContext.SocialActivityLikes.AsNoTracking()
                .Where(like => like.UserId == viewerId && activityIds.Contains(like.ActivityId))
                .Select(like => like.ActivityId)
                .ToHashSetAsync(cancellationToken);
        }

        return activityByReview.ToDictionary(
            pair => pair.Key,
            pair => new SocialReviewEngagement(
                pair.Key,
                pair.Value.Id,
                likes.GetValueOrDefault(pair.Value.Id),
                comments.GetValueOrDefault(pair.Value.Id),
                likedIds.Contains(pair.Value.Id)));
    }
}
