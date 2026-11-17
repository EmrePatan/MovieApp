using System.Text;
using Microsoft.EntityFrameworkCore;
using MovieApp.Application.Abstractions.Identity;
using MovieApp.Application.Abstractions.Social;
using MovieApp.Application.Exceptions;
using MovieApp.Application.Services.Identity;
using MovieApp.Domain.Enums;
using MovieApp.Infrastructure.Persistence;

namespace MovieApp.Infrastructure.Social;

public sealed class SocialFeedService(
    ApplicationDbContext dbContext,
    ICurrentUser currentUser,
    IUserAvatarPresentationService avatarPresentationService) : ISocialFeedService
{
    public Task<SocialFeedPageResult> GetFeedAsync(
        string? cursor, int pageSize, CancellationToken cancellationToken = default) =>
        GetFeedCoreAsync(null, cursor, pageSize, cancellationToken);

    public Task<SocialFeedPageResult> GetUserActivitiesAsync(
        Guid userId, string? cursor, int pageSize, CancellationToken cancellationToken = default) =>
        GetFeedCoreAsync(userId, cursor, pageSize, cancellationToken);

    private async Task<SocialFeedPageResult> GetFeedCoreAsync(
        Guid? actorId, string? cursor, int pageSize, CancellationToken cancellationToken)
    {
        if (!currentUser.IsAuthenticated || currentUser.UserId is not Guid viewerId || viewerId == Guid.Empty)
            throw new AuthenticationException("Authentication is required.");

        if (actorId.HasValue && !await dbContext.Users.AsNoTracking()
                .AnyAsync(user => user.Id == actorId.Value && user.IsActive, cancellationToken))
            throw new NotFoundException("User not found.");

        var safePageSize = Math.Clamp(pageSize, 1, 50);
        (DateTime CreatedAt, string Id)? decodedCursor = DecodeCursor(cursor);

        var query =
            from activity in dbContext.SocialActivities.AsNoTracking()
            join actor in dbContext.Users.AsNoTracking() on activity.ActorId equals actor.Id
            where !activity.IsDeleted && actor.IsActive
                && (!actorId.HasValue || actor.Id == actorId.Value)
                && (actor.Id == viewerId
                    || !dbContext.UserPrivacySettings.Any(settings => settings.UserId == actor.Id && settings.IsPrivate)
                    || dbContext.UserFollows.Any(follow => follow.FollowerId == viewerId
                        && follow.FollowedUserId == actor.Id && follow.Status == UserFollowStatus.Accepted))
                && !dbContext.UserBlocks.Any(block =>
                    (block.BlockerId == viewerId && block.BlockedUserId == actor.Id)
                    || (block.BlockerId == actor.Id && block.BlockedUserId == viewerId))
                && (actor.Id == viewerId
                    || !dbContext.UserPrivacySettings.Any(settings => settings.UserId == actor.Id)
                    || ((activity.ActivityType == SocialActivityType.WatchedMovie || activity.ActivityType == SocialActivityType.WatchedEpisode)
                        && dbContext.UserPrivacySettings.Where(settings => settings.UserId == actor.Id).Select(settings => settings.ShowWatched).FirstOrDefault())
                    || ((activity.ActivityType == SocialActivityType.RatedMovie || activity.ActivityType == SocialActivityType.RatedTvShow)
                        && dbContext.UserPrivacySettings.Where(settings => settings.UserId == actor.Id).Select(settings => settings.ShowRatings).FirstOrDefault())
                    || ((activity.ActivityType == SocialActivityType.FavoritedMovie || activity.ActivityType == SocialActivityType.FavoritedTvShow)
                        && dbContext.UserPrivacySettings.Where(settings => settings.UserId == actor.Id).Select(settings => settings.ShowFavorites).FirstOrDefault())
                    || ((activity.ActivityType == SocialActivityType.ReviewedMovie || activity.ActivityType == SocialActivityType.ReviewedTvShow)
                        && dbContext.UserPrivacySettings.Where(settings => settings.UserId == actor.Id).Select(settings => settings.ShowReviews).FirstOrDefault()))
            select new
            {
                Activity = activity,
                ActorId = actor.Id,
                actor.UserName,
                actor.DisplayName
            };

        if (decodedCursor.HasValue)
        {
            var cursorCreatedAt = decodedCursor.Value.CreatedAt;
            var cursorId = decodedCursor.Value.Id;
            query = query.Where(item => item.Activity.CreatedAt < cursorCreatedAt
                || (item.Activity.CreatedAt == cursorCreatedAt
                    && item.Activity.Id.ToString().CompareTo(cursorId) < 0));
        }

        var rows = await query
            .OrderByDescending(item => item.Activity.CreatedAt)
            .ThenByDescending(item => item.Activity.Id)
            .Take(safePageSize + 1)
            .ToListAsync(cancellationToken);

        var hasMore = rows.Count > safePageSize;
        if (hasMore) rows.RemoveAt(rows.Count - 1);
        if (rows.Count == 0) return new SocialFeedPageResult([], null);

        var movieIds = rows.Where(row => row.Activity.TargetContentType == "Movie")
            .Select(row => row.Activity.TargetContentId).Distinct().ToArray();
        var showIds = rows.Where(row => row.Activity.TargetContentType == "TvShow")
            .Select(row => row.Activity.TargetContentId).Distinct().ToArray();
        var episodeIds = rows.Where(row => row.Activity.TargetContentType == "Episode")
            .Select(row => row.Activity.TargetContentId).Distinct().ToArray();

        var movies = await dbContext.Movies.AsNoTracking().Where(movie => movieIds.Contains(movie.Id))
            .Select(movie => new { movie.Id, movie.Title, movie.PosterPath }).ToDictionaryAsync(movie => movie.Id, cancellationToken);
        var shows = await dbContext.TvShows.AsNoTracking().Where(show => showIds.Contains(show.Id))
            .Select(show => new { show.Id, show.Title, show.PosterPath }).ToDictionaryAsync(show => show.Id, cancellationToken);
        var episodes = await (
            from episode in dbContext.Episodes.AsNoTracking()
            join season in dbContext.Seasons.AsNoTracking() on episode.SeasonId equals season.Id
            join show in dbContext.TvShows.AsNoTracking() on season.TvShowId equals show.Id
            where episodeIds.Contains(episode.Id)
            select new
            {
                episode.Id,
                Title = show.Title,
                Subtitle = $"S{season.SeasonNumber} E{episode.EpisodeNumber} · {episode.Name}",
                show.PosterPath
            }).ToDictionaryAsync(episode => episode.Id, cancellationToken);

        var ratingIds = rows.Where(row => row.Activity.ActivityType is SocialActivityType.RatedMovie or SocialActivityType.RatedTvShow)
            .Select(row => row.Activity.SourceEntityId).Distinct().ToArray();
        var reviewIds = rows.Where(row => row.Activity.ActivityType is SocialActivityType.ReviewedMovie or SocialActivityType.ReviewedTvShow)
            .Select(row => row.Activity.SourceEntityId).Distinct().ToArray();
        var ratings = await dbContext.Ratings.AsNoTracking().Where(rating => ratingIds.Contains(rating.Id))
            .ToDictionaryAsync(rating => rating.Id, rating => rating.Score, cancellationToken);
        var reviews = await dbContext.Reviews.AsNoTracking().Where(review => reviewIds.Contains(review.Id))
            .ToDictionaryAsync(review => review.Id, review => review.Content, cancellationToken);

        var actorIds = rows.Select(row => row.ActorId).Distinct().ToArray();
        var activityIds = rows.Select(row => row.Activity.Id).ToArray();
        var avatars = await avatarPresentationService.GetForUsersAsync(actorIds, cancellationToken);
        var likes = await dbContext.SocialActivityLikes.AsNoTracking().Where(like => activityIds.Contains(like.ActivityId))
            .GroupBy(like => like.ActivityId).Select(group => new { Id = group.Key, Count = group.Count() })
            .ToDictionaryAsync(item => item.Id, item => item.Count, cancellationToken);
        var comments = await dbContext.SocialComments.AsNoTracking()
            .Where(comment => activityIds.Contains(comment.ActivityId) && !comment.IsDeleted)
            .GroupBy(comment => comment.ActivityId).Select(group => new { Id = group.Key, Count = group.Count() })
            .ToDictionaryAsync(item => item.Id, item => item.Count, cancellationToken);
        var likedIds = await dbContext.SocialActivityLikes.AsNoTracking()
            .Where(like => like.UserId == viewerId && activityIds.Contains(like.ActivityId))
            .Select(like => like.ActivityId).ToHashSetAsync(cancellationToken);
        var items = new List<SocialFeedItemResult>(rows.Count);
        foreach (var row in rows)
        {
            var activity = row.Activity;
            var title = string.Empty;
            string? subtitle = null;
            string? poster = null;
            if (activity.TargetContentType == "Movie" && movies.TryGetValue(activity.TargetContentId, out var movie))
            {
                title = movie.Title;
                poster = movie.PosterPath;
            }
            else if (activity.TargetContentType == "TvShow" && shows.TryGetValue(activity.TargetContentId, out var show))
            {
                title = show.Title;
                poster = show.PosterPath;
            }
            else if (activity.TargetContentType == "Episode" && episodes.TryGetValue(activity.TargetContentId, out var episode))
            {
                title = episode.Title;
                subtitle = episode.Subtitle;
                poster = episode.PosterPath;
            }

            items.Add(new SocialFeedItemResult(
                activity.Id, row.ActorId, row.UserName, row.DisplayName,
                avatars.TryGetValue(row.ActorId, out var avatar) ? avatar.EffectiveAvatarUrl : null,
                activity.ActivityType, activity.CreatedAt, activity.TargetContentId,
                activity.TargetContentType, title, subtitle, poster,
                ratings.TryGetValue(activity.SourceEntityId, out var ratingScore) ? ratingScore : null,
                reviews.TryGetValue(activity.SourceEntityId, out var review) ? review : null,
                likes.GetValueOrDefault(activity.Id), comments.GetValueOrDefault(activity.Id), likedIds.Contains(activity.Id)));
        }

        var nextCursor = hasMore && rows.Count > 0
            ? EncodeCursor(rows[^1].Activity.CreatedAt, rows[^1].Activity.Id)
            : null;
        return new SocialFeedPageResult(items, nextCursor);
    }

    private static (DateTime CreatedAt, string Id)? DecodeCursor(string? cursor)
    {
        if (string.IsNullOrWhiteSpace(cursor)) return null;
        try
        {
            var parts = Encoding.UTF8.GetString(Convert.FromBase64String(cursor)).Split('|', 2);
            if (parts.Length != 2 || !long.TryParse(parts[0], out var ticks) || !Guid.TryParse(parts[1], out var id))
                throw new FormatException();
            return (new DateTime(ticks, DateTimeKind.Utc), id.ToString("D"));
        }
        catch (Exception exception) when (exception is FormatException or ArgumentException)
        {
            throw new ValidationException("Invalid social feed cursor.");
        }
    }

    private static string EncodeCursor(DateTime createdAt, Guid id) =>
        Convert.ToBase64String(Encoding.UTF8.GetBytes($"{createdAt.Ticks}|{id:D}"));
}
