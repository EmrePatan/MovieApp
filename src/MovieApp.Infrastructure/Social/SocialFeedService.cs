using System.Text;
using Microsoft.EntityFrameworkCore;
using MovieApp.Application.Abstractions.Identity;
using MovieApp.Application.Abstractions.Persistence;
using MovieApp.Application.Abstractions.Social;
using MovieApp.Application.Exceptions;
using MovieApp.Application.Models.Search;
using MovieApp.Application.Services.Identity;
using MovieApp.Application.Services.Localization;
using MovieApp.Domain.Entities;
using MovieApp.Domain.Enums;
using MovieApp.Infrastructure.Persistence;

namespace MovieApp.Infrastructure.Social;

public sealed class SocialFeedService(
    ApplicationDbContext dbContext,
    ICurrentUser currentUser,
    IUserAvatarPresentationService avatarPresentationService,
    ISummaryLocalizationMetadataReadRepository summaryLocalizationMetadataReadRepository) : ISocialFeedService
{
    public Task<SocialFeedPageResult> GetFeedAsync(
        string? cursor,
        int pageSize,
        SocialFeedMode mode = SocialFeedMode.Mixed,
        string? contentLocale = null,
        CancellationToken cancellationToken = default) =>
        GetFeedCoreAsync(null, cursor, pageSize, cancellationToken, activityId: null, feedMode: mode, contentLocale: contentLocale);

    public async Task<SocialFeedPageResult> GetUserActivitiesAsync(
        Guid userId,
        string? cursor,
        int pageSize,
        string? contentLocale = null,
        CancellationToken cancellationToken = default)
    {
        await EnsureProfileActivitiesVisibleAsync(userId, cancellationToken);
        return await GetFeedCoreAsync(userId, cursor, pageSize, cancellationToken, contentLocale: contentLocale);
    }

    public async Task<SocialFeedItemResult> GetActivityAsync(
        Guid activityId,
        string? contentLocale = null,
        CancellationToken cancellationToken = default)
    {
        if (activityId == Guid.Empty)
            throw new NotFoundException("Activity not found.");

        var page = await GetFeedCoreAsync(null, null, 1, cancellationToken, activityId, contentLocale: contentLocale);
        var item = page.Items.FirstOrDefault(item => item.Id == activityId);
        if (item is null)
            throw new NotFoundException("Activity not found.");
        return item;
    }

    private async Task<SocialFeedPageResult> GetFeedCoreAsync(
        Guid? actorId,
        string? cursor,
        int pageSize,
        CancellationToken cancellationToken,
        Guid? activityId = null,
        SocialFeedMode feedMode = SocialFeedMode.Mixed,
        string? contentLocale = null)
    {
        if (!currentUser.IsAuthenticated || currentUser.UserId is not Guid viewerId || viewerId == Guid.Empty)
            throw new AuthenticationException("Authentication is required.");

        if (actorId.HasValue && !await dbContext.Users.AsNoTracking()
                .AnyAsync(user => user.Id == actorId.Value && user.IsActive, cancellationToken))
            throw new NotFoundException("User not found.");

        var safePageSize = Math.Clamp(pageSize, 1, 50);
        var isHomeFeed = !actorId.HasValue && !activityId.HasValue;
        var discoveryCutoff = DateTime.UtcNow.AddDays(-14);
        var spreadActors = isHomeFeed && feedMode is SocialFeedMode.Mixed or SocialFeedMode.Discover;
        (DateTime CreatedAt, string Id)? decodedCursor = DecodeCursor(cursor);

        var query =
            from activity in dbContext.SocialActivities.AsNoTracking()
            join actor in dbContext.Users.AsNoTracking() on activity.ActorId equals actor.Id
            where !activity.IsDeleted && actor.IsActive
                && (!activityId.HasValue || activity.Id == activityId.Value)
                && (!actorId.HasValue || actor.Id == actorId.Value)
                // Home feed (Keşfet / Takip) is written-review-only; profile & single-activity keep full history.
                && (!isHomeFeed
                    || ((activity.ActivityType == SocialActivityType.ReviewedMovie
                            || activity.ActivityType == SocialActivityType.ReviewedTvShow
                            || activity.ActivityType == SocialActivityType.ReviewedEpisode)
                        && dbContext.Reviews.Any(review =>
                            review.Id == activity.SourceEntityId
                            && review.Content != null
                            && review.Content != "")))
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
                    || ((activity.ActivityType == SocialActivityType.RatedMovie || activity.ActivityType == SocialActivityType.RatedTvShow || activity.ActivityType == SocialActivityType.RatedEpisode)
                        && dbContext.UserPrivacySettings.Where(settings => settings.UserId == actor.Id).Select(settings => settings.ShowRatings).FirstOrDefault())
                    || ((activity.ActivityType == SocialActivityType.FavoritedMovie || activity.ActivityType == SocialActivityType.FavoritedTvShow)
                        && dbContext.UserPrivacySettings.Where(settings => settings.UserId == actor.Id).Select(settings => settings.ShowFavorites).FirstOrDefault())
                    || ((activity.ActivityType == SocialActivityType.ReviewedMovie || activity.ActivityType == SocialActivityType.ReviewedTvShow || activity.ActivityType == SocialActivityType.ReviewedEpisode)
                        && dbContext.UserPrivacySettings.Where(settings => settings.UserId == actor.Id).Select(settings => settings.ShowReviews).FirstOrDefault()))
                && (!isHomeFeed
                    || (feedMode == SocialFeedMode.Following
                        && (actor.Id == viewerId
                            || dbContext.UserFollows.Any(follow => follow.FollowerId == viewerId
                                && follow.FollowedUserId == actor.Id
                                && follow.Status == UserFollowStatus.Accepted)))
                    || (feedMode == SocialFeedMode.Discover
                        && actor.Id != viewerId
                        && !dbContext.UserFollows.Any(follow => follow.FollowerId == viewerId
                            && follow.FollowedUserId == actor.Id
                            && follow.Status == UserFollowStatus.Accepted)
                        && activity.CreatedAt >= discoveryCutoff)
                    || (feedMode == SocialFeedMode.Mixed
                        && (actor.Id == viewerId
                            || dbContext.UserFollows.Any(follow => follow.FollowerId == viewerId
                                && follow.FollowedUserId == actor.Id
                                && follow.Status == UserFollowStatus.Accepted)
                            || activity.CreatedAt >= discoveryCutoff)))
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

        // The cursor is the oldest row of the page, so it is taken before any reordering.
        var nextCursor = hasMore
            ? EncodeCursor(rows[^1].Activity.CreatedAt, rows[^1].Activity.Id)
            : null;
        if (spreadActors)
            rows = SpreadActors(rows, maxPerActor: 3, row => row.ActorId);

        var movieIds = rows.Where(row => row.Activity.TargetContentType == "Movie")
            .Select(row => row.Activity.TargetContentId).Distinct().ToArray();
        var showIds = rows.Where(row => row.Activity.TargetContentType == "TvShow")
            .Select(row => row.Activity.TargetContentId).Distinct().ToArray();
        var episodeIds = rows.Where(row => row.Activity.TargetContentType == "Episode")
            .Select(row => row.Activity.TargetContentId).Distinct().ToArray();

        var movies = await dbContext.Movies.AsNoTracking().Where(movie => movieIds.Contains(movie.Id))
            .Select(movie => new
            {
                movie.Id,
                movie.Title,
                movie.OriginalTitle,
                movie.OriginalLanguage,
                movie.PrimaryOriginCountryCode,
                movie.PosterPath
            }).ToDictionaryAsync(movie => movie.Id, cancellationToken);
        var shows = await dbContext.TvShows.AsNoTracking().Where(show => showIds.Contains(show.Id))
            .Select(show => new
            {
                show.Id,
                show.Title,
                show.OriginalTitle,
                show.OriginalLanguage,
                show.PrimaryOriginCountryCode,
                show.PosterPath
            }).ToDictionaryAsync(show => show.Id, cancellationToken);
        var episodes = await (
            from episode in dbContext.Episodes.AsNoTracking()
            join season in dbContext.Seasons.AsNoTracking() on episode.SeasonId equals season.Id
            join show in dbContext.TvShows.AsNoTracking() on season.TvShowId equals show.Id
            where episodeIds.Contains(episode.Id)
            select new
            {
                episode.Id,
                ShowId = show.Id,
                SeasonNumber = season.SeasonNumber,
                EpisodeNumber = episode.EpisodeNumber,
                Title = show.Title,
                OriginalTitle = show.OriginalTitle,
                OriginalLanguage = show.OriginalLanguage,
                PrimaryOriginCountryCode = show.PrimaryOriginCountryCode,
                Subtitle = $"S{season.SeasonNumber} E{episode.EpisodeNumber} · {episode.Name}",
                show.PosterPath
            }).ToDictionaryAsync(episode => episode.Id, cancellationToken);

        var resolvedLocale = ContentLocaleResolver.Normalize(contentLocale);
        IReadOnlyDictionary<CatalogContentKey, string> localizedMovieTitles =
            new Dictionary<CatalogContentKey, string>();
        IReadOnlyDictionary<CatalogContentKey, string> localizedTvTitles =
            new Dictionary<CatalogContentKey, string>();
        if (ContentLocaleResolver.RequiresLocalization(resolvedLocale))
        {
            var localizationMovieIds = movieIds.ToList();
            var localizationTvIds = showIds
                .Concat(episodes.Values.Select(episode => episode.ShowId))
                .Distinct()
                .ToList();
            if (localizationMovieIds.Count > 0 || localizationTvIds.Count > 0)
            {
                var metadata = await summaryLocalizationMetadataReadRepository.LoadAsync(
                    localizationMovieIds,
                    localizationTvIds,
                    [],
                    resolvedLocale,
                    cancellationToken);
                localizedMovieTitles = metadata.LocalizedMovieTitles;
                localizedTvTitles = metadata.LocalizedTvTitles;
            }
        }

        var ratingIds = rows.Where(row => row.Activity.ActivityType is SocialActivityType.RatedMovie or SocialActivityType.RatedTvShow or SocialActivityType.RatedEpisode)
            .Select(row => row.Activity.SourceEntityId).Distinct().ToArray();
        var reviewIds = rows.Where(row => row.Activity.ActivityType is SocialActivityType.ReviewedMovie or SocialActivityType.ReviewedTvShow or SocialActivityType.ReviewedEpisode)
            .Select(row => row.Activity.SourceEntityId).Distinct().ToArray();
        var ratings = await dbContext.Ratings.AsNoTracking().Where(rating => ratingIds.Contains(rating.Id))
            .ToDictionaryAsync(rating => rating.Id, rating => rating.Score, cancellationToken);
        var reviews = await dbContext.Reviews.AsNoTracking()
            .Where(review => reviewIds.Contains(review.Id))
            .ToDictionaryAsync(review => review.Id, cancellationToken);

        var actorIds = rows.Select(row => row.ActorId).Distinct().ToArray();
        var activityIds = rows.Select(row => row.Activity.Id).ToArray();
        var avatars = await avatarPresentationService.GetForUsersAsync(actorIds, cancellationToken);
        var likes = await (
            from like in dbContext.SocialActivityLikes.AsNoTracking()
            join user in dbContext.Users.AsNoTracking() on like.UserId equals user.Id
            where activityIds.Contains(like.ActivityId) && user.IsActive
                && !dbContext.UserBlocks.Any(block =>
                    (block.BlockerId == viewerId && block.BlockedUserId == like.UserId)
                    || (block.BlockerId == like.UserId && block.BlockedUserId == viewerId))
            group like by like.ActivityId into grouped
            select new { Id = grouped.Key, Count = grouped.Count() })
            .ToDictionaryAsync(item => item.Id, item => item.Count, cancellationToken);
        var comments = await (
            from comment in dbContext.SocialComments.AsNoTracking()
            join author in dbContext.Users.AsNoTracking() on comment.AuthorId equals author.Id
            where activityIds.Contains(comment.ActivityId)
                && comment.ParentCommentId == null
                && !comment.IsDeleted && author.IsActive
                && !dbContext.UserBlocks.Any(block =>
                    (block.BlockerId == viewerId && block.BlockedUserId == comment.AuthorId)
                    || (block.BlockerId == comment.AuthorId && block.BlockedUserId == viewerId))
            group comment by comment.ActivityId into grouped
            select new { Id = grouped.Key, Count = grouped.Count() })
            .ToDictionaryAsync(item => item.Id, item => item.Count, cancellationToken);
        var likedIds = await dbContext.SocialActivityLikes.AsNoTracking()
            .Where(like => like.UserId == viewerId && activityIds.Contains(like.ActivityId))
            .Select(like => like.ActivityId).ToHashSetAsync(cancellationToken);
        var highlightComments = await BuildHighlightCommentsAsync(activityIds, viewerId, cancellationToken);
        var episodeShowIds = episodes.ToDictionary(pair => pair.Key, pair => pair.Value.ShowId);
        var activitiesById = rows.ToDictionary(row => row.Activity.Id, row => row.Activity);
        var catalogRatingKeys = new List<(Guid UserId, string TargetContentType, Guid TargetContentId)>();
        foreach (var row in rows)
            catalogRatingKeys.Add((row.ActorId, row.Activity.TargetContentType, row.Activity.TargetContentId));
        foreach (var (highlightedActivityId, highlight) in highlightComments)
        {
            if (!activitiesById.TryGetValue(highlightedActivityId, out var ratedActivity)) continue;
            catalogRatingKeys.Add((highlight.UserId,ratedActivity.TargetContentType, ratedActivity.TargetContentId));
        }

        var catalogRatings = await LoadUserCatalogRatingsAsync(catalogRatingKeys, episodeShowIds, cancellationToken);
        var items = new List<SocialFeedItemResult>(rows.Count);
        foreach (var row in rows)
        {
            var activity = row.Activity;
            var title = string.Empty;
            string? originalTitle = null;
            string? subtitle = null;
            string? poster = null;
            if (activity.TargetContentType == "Movie" && movies.TryGetValue(activity.TargetContentId, out var movie))
            {
                localizedMovieTitles.TryGetValue(new CatalogContentKey(movie.Id, "movie"), out var localizedTitle);
                (title, originalTitle) = LocalizedDisplayTitleSelector.ChooseDisplayTitles(
                    movie.Title,
                    movie.OriginalTitle,
                    movie.OriginalLanguage,
                    localizedTitle,
                    resolvedLocale,
                    movie.PrimaryOriginCountryCode);
                poster = movie.PosterPath;
            }
            else if (activity.TargetContentType == "TvShow" && shows.TryGetValue(activity.TargetContentId, out var show))
            {
                localizedTvTitles.TryGetValue(new CatalogContentKey(show.Id, "tv"), out var localizedTitle);
                (title, originalTitle) = LocalizedDisplayTitleSelector.ChooseDisplayTitles(
                    show.Title,
                    show.OriginalTitle,
                    show.OriginalLanguage,
                    localizedTitle,
                    resolvedLocale,
                    show.PrimaryOriginCountryCode);
                poster = show.PosterPath;
            }
            else if (activity.TargetContentType == "Episode" && episodes.TryGetValue(activity.TargetContentId, out var episode))
            {
                localizedTvTitles.TryGetValue(new CatalogContentKey(episode.ShowId, "tv"), out var localizedTitle);
                (title, originalTitle) = LocalizedDisplayTitleSelector.ChooseDisplayTitles(
                    episode.Title,
                    episode.OriginalTitle,
                    episode.OriginalLanguage,
                    localizedTitle,
                    resolvedLocale,
                    episode.PrimaryOriginCountryCode);
                subtitle = episode.Subtitle;
                poster = episode.PosterPath;
            }

            Guid? catalogNavigationId = null;
            string? catalogNavigationType = null;
            int? catalogSeasonNumber = null;
            int? catalogEpisodeNumber = null;
            if (activity.TargetContentType == "Movie")
            {
                catalogNavigationId = activity.TargetContentId;
                catalogNavigationType = "movie";
            }
            else if (activity.TargetContentType == "TvShow")
            {
                catalogNavigationId = activity.TargetContentId;
                catalogNavigationType = "tv";
            }
            else if (activity.TargetContentType == "Episode"
                && episodes.TryGetValue(activity.TargetContentId, out var episodeNav))
            {
                catalogNavigationId = episodeNav.ShowId;
                catalogNavigationType = "episode";
                catalogSeasonNumber = episodeNav.SeasonNumber;
                catalogEpisodeNumber = episodeNav.EpisodeNumber;
            }

            highlightComments.TryGetValue(activity.Id, out var highlight);
            Review? reviewRow = null;
            string? reviewExcerpt = null;
            if (activity.ActivityType is SocialActivityType.ReviewedMovie or SocialActivityType.ReviewedTvShow or SocialActivityType.ReviewedEpisode
                && reviews.TryGetValue(activity.SourceEntityId, out var loadedReview))
            {
                reviewRow = loadedReview;
                reviewExcerpt = TruncateReviewExcerpt(loadedReview.Content);
            }

            int? highlightAuthorRating = null;
            if (highlight is not null)
                highlightAuthorRating = ResolveUserCatalogRating(
                    catalogRatings, highlight.UserId, activity.TargetContentType, activity.TargetContentId, episodeShowIds);

            var actorCatalogRating = ResolveUserCatalogRating(
                catalogRatings, row.ActorId, activity.TargetContentType, activity.TargetContentId, episodeShowIds);
            int? ratingScore = activity.ActivityType is SocialActivityType.RatedMovie or SocialActivityType.RatedTvShow or SocialActivityType.RatedEpisode
                && ratings.TryGetValue(activity.SourceEntityId, out var entityRating)
                ? entityRating
                : actorCatalogRating;

            items.Add(new SocialFeedItemResult(
                activity.Id, row.ActorId, row.UserName, row.DisplayName,
                avatars.TryGetValue(row.ActorId, out var avatar) ? avatar.EffectiveAvatarUrl : null,
                activity.ActivityType.ToString(), activity.CreatedAt, activity.TargetContentId,
                activity.TargetContentType, title, originalTitle, subtitle, poster,
                ratingScore,
                reviewExcerpt,
                likes.GetValueOrDefault(activity.Id), comments.GetValueOrDefault(activity.Id), likedIds.Contains(activity.Id),
                catalogNavigationId, catalogNavigationType,
                catalogSeasonNumber, catalogEpisodeNumber,
                reviewRow?.Id, reviewRow?.AuthoringLocale, reviewRow?.UpdatedAt,
                // Only top-level comments are highlighted, so there is never a parent to show.
                highlight, null, null,
                highlightAuthorRating));
        }

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

    private async Task<Dictionary<Guid, SocialCommentResult>> BuildHighlightCommentsAsync(
        Guid[] activityIds, Guid viewerId, CancellationToken cancellationToken)
    {
        if (activityIds.Length == 0) return [];

        var visibleComments =
            from comment in dbContext.SocialComments.AsNoTracking()
            where activityIds.Contains(comment.ActivityId)
                && comment.ParentCommentId == null
                && !comment.IsDeleted
                && dbContext.Users.Any(user => user.Id == comment.AuthorId && user.IsActive)
                && !dbContext.UserBlocks.Any(block =>
                    (block.BlockerId == viewerId && block.BlockedUserId == comment.AuthorId)
                    || (block.BlockerId == comment.AuthorId && block.BlockedUserId == viewerId))
            select comment;

        // One row per activity leaves the database, however many comments it has.
        var latestIds = visibleComments
            .GroupBy(comment => comment.ActivityId)
            .Select(group => group
                .OrderByDescending(comment => comment.CreatedAt)
                .ThenByDescending(comment => comment.Id)
                .Select(comment => comment.Id)
                .First());

        var latestRows = await (
            from comment in dbContext.SocialComments.AsNoTracking()
            join user in dbContext.Users.AsNoTracking() on comment.AuthorId equals user.Id
            where latestIds.Contains(comment.Id)
            select new
            {
                comment.Id, comment.ActivityId, comment.AuthorId, user.UserName, user.DisplayName,
                comment.ParentCommentId, comment.Content, comment.AuthoringLocale, comment.CreatedAt, comment.UpdatedAt
            }).ToListAsync(cancellationToken);

        if (latestRows.Count == 0) return [];

        var latestByActivity = latestRows.ToDictionary(row => row.ActivityId);

        var commentIds = latestByActivity.Values.Select(row => row.Id).ToArray();
        var authorIds = latestByActivity.Values.Select(row => row.AuthorId).Distinct().ToArray();
        var avatars = await avatarPresentationService.GetForUsersAsync(authorIds, cancellationToken);
        var likes = await (
            from like in dbContext.SocialCommentLikes.AsNoTracking()
            join user in dbContext.Users.AsNoTracking() on like.UserId equals user.Id
            where commentIds.Contains(like.CommentId) && user.IsActive
                && !dbContext.UserBlocks.Any(block =>
                    (block.BlockerId == viewerId && block.BlockedUserId == like.UserId)
                    || (block.BlockerId == like.UserId && block.BlockedUserId == viewerId))
            group like by like.CommentId into grouped
            select new { Id = grouped.Key, Count = grouped.Count() })
            .ToDictionaryAsync(item => item.Id, item => item.Count, cancellationToken);
        var replies = await (
            from comment in dbContext.SocialComments.AsNoTracking()
            join user in dbContext.Users.AsNoTracking() on comment.AuthorId equals user.Id
            where comment.ParentCommentId != null && commentIds.Contains(comment.ParentCommentId.Value)
                && !comment.IsDeleted && user.IsActive
                && !dbContext.UserBlocks.Any(block =>
                    (block.BlockerId == viewerId && block.BlockedUserId == comment.AuthorId)
                    || (block.BlockerId == comment.AuthorId && block.BlockedUserId == viewerId))
            group comment by comment.ParentCommentId!.Value into grouped
            select new { Id = grouped.Key, Count = grouped.Count() })
            .ToDictionaryAsync(item => item.Id, item => item.Count, cancellationToken);
        var likedIds = await dbContext.SocialCommentLikes.AsNoTracking()
            .Where(like => like.UserId == viewerId && commentIds.Contains(like.CommentId))
            .Select(like => like.CommentId).ToHashSetAsync(cancellationToken);

        return latestByActivity.ToDictionary(
            pair => pair.Key,
            pair =>
            {
                var row = pair.Value;
                return new SocialCommentResult(
                    row.Id, row.ActivityId, row.AuthorId, row.UserName, row.DisplayName,
                    avatars.TryGetValue(row.AuthorId, out var avatar) ? avatar.EffectiveAvatarUrl : null,
                    row.ParentCommentId, row.Content, row.CreatedAt, row.UpdatedAt,
                    likes.GetValueOrDefault(row.Id), replies.GetValueOrDefault(row.Id), likedIds.Contains(row.Id),
                    row.AuthoringLocale);
            });
    }

    private async Task<Dictionary<(Guid UserId, string TargetContentType, Guid TargetContentId), int>> LoadUserCatalogRatingsAsync(
        List<(Guid UserId, string TargetContentType, Guid TargetContentId)> lookups,
        Dictionary<Guid, Guid> episodeShowIds,
        CancellationToken cancellationToken)
    {
        if (lookups.Count == 0) return [];

        var userIds = lookups.Select(lookup => lookup.UserId).Distinct().ToArray();
        var movieIds = lookups
            .Where(lookup => lookup.TargetContentType == "Movie")
            .Select(lookup => lookup.TargetContentId)
            .Distinct()
            .ToArray();
        var episodeTargetIds = lookups
            .Where(lookup => lookup.TargetContentType == "Episode")
            .Select(lookup => lookup.TargetContentId)
            .Distinct()
            .ToArray();
        var tvShowIds = lookups
            .Select(lookup => lookup.TargetContentType switch
            {
                "TvShow" => (Guid?)lookup.TargetContentId,
                "Episode" when episodeShowIds.TryGetValue(lookup.TargetContentId, out var showId) => showId,
                _ => null
            })
            .Where(id => id.HasValue)
            .Select(id => id!.Value)
            .Distinct()
            .ToArray();

        var ratingRows = await dbContext.Ratings.AsNoTracking()
            .Where(rating => userIds.Contains(rating.UserId)
                && ((rating.MovieId != null && movieIds.Contains(rating.MovieId.Value))
                    || (rating.TvShowId != null && tvShowIds.Contains(rating.TvShowId.Value))
                    || (rating.EpisodeId != null && episodeTargetIds.Contains(rating.EpisodeId.Value))))
            .Select(rating => new { rating.UserId, rating.MovieId, rating.TvShowId, rating.EpisodeId, rating.Score })
            .ToListAsync(cancellationToken);

        var movieRatings = ratingRows
            .Where(rating => rating.MovieId is not null)
            .ToDictionary(rating => (rating.UserId, rating.MovieId!.Value), rating => rating.Score);
        var tvRatings = ratingRows
            .Where(rating => rating.TvShowId is not null)
            .ToDictionary(rating => (rating.UserId, rating.TvShowId!.Value), rating => rating.Score);
        var episodeRatings = ratingRows
            .Where(rating => rating.EpisodeId is not null)
            .ToDictionary(rating => (rating.UserId, rating.EpisodeId!.Value), rating => rating.Score);

        var result = new Dictionary<(Guid UserId, string TargetContentType, Guid TargetContentId), int>();
        foreach (var lookup in lookups.Distinct())
        {
            int? matched = lookup.TargetContentType switch
            {
                "Movie" => movieRatings.TryGetValue((lookup.UserId, lookup.TargetContentId), out var movieScore)
                    ? movieScore
                    : null,
                "TvShow" => tvRatings.TryGetValue((lookup.UserId, lookup.TargetContentId), out var tvScore)
                    ? tvScore
                    : null,
                "Episode" when episodeRatings.TryGetValue((lookup.UserId, lookup.TargetContentId), out var directEpisodeScore)
                    => directEpisodeScore,
                "Episode" when episodeShowIds.TryGetValue(lookup.TargetContentId, out var showId)
                    && tvRatings.TryGetValue((lookup.UserId, showId), out var showScore)
                    => showScore,
                _ => null
            };

            if (matched is > 0)
                result[(lookup.UserId, lookup.TargetContentType, lookup.TargetContentId)] = matched.Value;
        }

        return result;
    }

    private static int? ResolveUserCatalogRating(
        Dictionary<(Guid UserId, string TargetContentType, Guid TargetContentId), int> catalogRatings,
        Guid userId,
        string targetContentType,
        Guid targetContentId,
        Dictionary<Guid, Guid> episodeShowIds)
    {
        if (catalogRatings.TryGetValue((userId, targetContentType, targetContentId), out var direct))
            return direct;

        if (targetContentType == "Episode"
            && episodeShowIds.TryGetValue(targetContentId, out var showId)
            && catalogRatings.TryGetValue((userId, "TvShow", showId), out var viaShow))
            return viaShow;

        return null;
    }

    private async Task EnsureProfileActivitiesVisibleAsync(Guid userId, CancellationToken cancellationToken)
    {
        if (!currentUser.IsAuthenticated || currentUser.UserId is not Guid viewerId || viewerId == Guid.Empty)
            throw new AuthenticationException("Authentication is required.");
        if (userId == viewerId) return;

        var isPrivate = await dbContext.UserPrivacySettings.AsNoTracking()
            .Where(settings => settings.UserId == userId)
            .Select(settings => (bool?)settings.IsPrivate)
            .FirstOrDefaultAsync(cancellationToken) ?? false;
        if (!isPrivate) return;

        var accepted = await dbContext.UserFollows.AsNoTracking().AnyAsync(
            follow => follow.FollowerId == viewerId && follow.FollowedUserId == userId
                && follow.Status == UserFollowStatus.Accepted, cancellationToken);
        if (!accepted) throw new NotFoundException("User not found.");
    }

    private static string? TruncateReviewExcerpt(string content)
    {
        if (string.IsNullOrWhiteSpace(content)) return null;
        const int maxLength = 280;
        return content.Length <= maxLength ? content : $"{content[..(maxLength - 1)]}…";
    }

    /// <summary>
    /// Moves each actor's rows beyond <paramref name="maxPerActor"/> to the end of the page.
    /// Nothing is dropped: a row left out of a page would be older than no later cursor.
    /// </summary>
    internal static List<T> SpreadActors<T>(List<T> rows, int maxPerActor, Func<T, Guid> actorIdSelector)
    {
        var counts = new Dictionary<Guid, int>();
        var leading = new List<T>(rows.Count);
        var overflow = new List<T>();
        foreach (var row in rows)
        {
            var actorId = actorIdSelector(row);
            var seen = counts.GetValueOrDefault(actorId);
            counts[actorId] = seen + 1;
            (seen < maxPerActor ? leading : overflow).Add(row);
        }

        leading.AddRange(overflow);
        return leading;
    }
}
