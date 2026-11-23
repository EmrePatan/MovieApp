using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using MovieApp.Application.Abstractions.Identity;
using MovieApp.Application.Abstractions.Persistence;
using MovieApp.Application.Abstractions.Social;
using MovieApp.Application.Configuration;
using MovieApp.Application.Exceptions;
using MovieApp.Application.Models.Library;
using MovieApp.Application.Models.Search;
using MovieApp.Application.Mapping;
using MovieApp.Application.Models.Identity;
using MovieApp.Application.Models.Watchlists;
using MovieApp.Application.Services.Identity;
using MovieApp.Application.Validation;
using MovieApp.Domain.Enums;
using MovieApp.Infrastructure.Persistence;

namespace MovieApp.Infrastructure.Social;

public sealed class SocialProfileService(
    ApplicationDbContext dbContext,
    ICurrentUser currentUser,
    IUserAvatarPresentationService avatarPresentationService,
    IUserStatisticsRepository userStatisticsRepository,
    IUserAvatarService userAvatarService,
    ILibraryRepository libraryRepository,
    IOptions<AvatarStorageOptions> avatarStorageOptions,
    IWatchlistRepository watchlistRepository,
    IWatchlistItemRepository watchlistItemRepository) : ISocialProfileService
{
    private const int PublicLibraryShelfPageSize = 48;

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

        var isSelf = profile.FollowStatus == "Self";
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

        var movieRuntimeMinutes = 0;
        var episodeRuntimeMinutes = 0;
        IReadOnlyList<SocialProfileShelfItemResult> favoriteMovies = [];
        IReadOnlyList<SocialProfileShelfItemResult> favoriteShows = [];

        if (showWatched)
        {
            movieRuntimeMinutes = await dbContext.WatchedMovies.AsNoTracking()
                .Where(watched => watched.UserId == userId)
                .Join(dbContext.Movies.AsNoTracking(), watched => watched.MovieId, movie => movie.Id, (watched, movie) => movie.RuntimeMinutes)
                .SumAsync(minutes => minutes ?? 0, cancellationToken);
            episodeRuntimeMinutes = await dbContext.WatchedEpisodes.AsNoTracking()
                .Where(watched => watched.UserId == userId)
                .Join(dbContext.Episodes.AsNoTracking(), watched => watched.EpisodeId, episode => episode.Id, (watched, episode) => episode.RuntimeMinutes)
                .SumAsync(minutes => minutes ?? 0, cancellationToken);
        }

        if (showFavorites)
        {
            favoriteMovies = await (
                from favorite in dbContext.Favorites.AsNoTracking()
                join movie in dbContext.Movies.AsNoTracking() on favorite.MovieId equals movie.Id
                where favorite.UserId == userId
                orderby favorite.CreatedAt descending
                select new SocialProfileShelfItemResult(movie.Id, movie.Title, movie.PosterPath, "Movie"))
                .Take(8)
                .ToListAsync(cancellationToken);
            favoriteShows = await (
                from favorite in dbContext.Favorites.AsNoTracking()
                join show in dbContext.TvShows.AsNoTracking() on favorite.TvShowId equals show.Id
                where favorite.UserId == userId
                orderby favorite.CreatedAt descending
                select new SocialProfileShelfItemResult(show.Id, show.Title, show.PosterPath, "TvShow"))
                .Take(8)
                .ToListAsync(cancellationToken);
        }

        IReadOnlyList<SocialProfileShelfItemResult> watchingTitles = [];
        IReadOnlyList<SocialProfileShelfItemResult> watchedTitles = [];
        IReadOnlyList<SocialProfileShelfItemResult> likedTitles = [];
        IReadOnlyList<SocialProfileShelfItemResult> watchlistTitles = [];

        if (isSelf || showWatched)
        {
            watchingTitles = await LoadCategoryShelfAsync(userId, LibraryCategory.Watching, cancellationToken);
            watchedTitles = await LoadCategoryShelfAsync(userId, LibraryCategory.Watched, cancellationToken);
            watchlistTitles = await LoadCategoryShelfAsync(userId, LibraryCategory.Watchlist, cancellationToken);
        }

        if (isSelf || showFavorites)
        {
            likedTitles = await LoadCategoryShelfAsync(userId, LibraryCategory.Liked, cancellationToken);
        }

        var libraryTitles = await LoadLibraryTitlesAsync(
            userId,
            isSelf || showWatched,
            isSelf || showFavorites,
            cancellationToken);

        return new SocialProfileStatisticsResult(
            showWatched ? statistics.Summary.MoviesWatched : 0,
            showWatched ? statistics.Summary.EpisodesWatched : 0,
            showRatings ? statistics.Summary.RatingsCount : 0,
            showReviews ? statistics.Summary.ReviewsCount : 0,
            showFavorites ? statistics.Summary.FavoritesCount : 0,
            milestones,
            movieRuntimeMinutes,
            episodeRuntimeMinutes,
            favoriteMovies,
            favoriteShows,
            libraryTitles,
            watchingTitles,
            watchedTitles,
            likedTitles,
            watchlistTitles);
    }

    private async Task<IReadOnlyList<SocialProfileShelfItemResult>> LoadCategoryShelfAsync(
        Guid userId,
        LibraryCategory category,
        CancellationToken cancellationToken)
    {
        var request = new LibraryPageRequest(
            1,
            PublicLibraryShelfPageSize,
            PublicLibraryShelfPageSize + 1,
            null,
            LibraryCountMode.Skip);

        var (items, _) = category switch
        {
            LibraryCategory.Watching => await libraryRepository.GetWatchingAsync(
                userId, SearchContentType.All, request, cancellationToken),
            LibraryCategory.Watched => await libraryRepository.GetWatchedAsync(
                userId, SearchContentType.All, request, cancellationToken),
            LibraryCategory.Liked => await libraryRepository.GetLikedAsync(
                userId, SearchContentType.All, request, cancellationToken),
            LibraryCategory.Watchlist => await libraryRepository.GetWatchlistAsync(
                userId, SearchContentType.All, request, cancellationToken),
            _ => ([], 0),
        };

        return items
            .Take(PublicLibraryShelfPageSize)
            .Select(MapLibraryItemToShelf)
            .ToList();
    }

    private static SocialProfileShelfItemResult MapLibraryItemToShelf(LibraryItemResult item)
    {
        var contentType = string.Equals(item.Type, "tv", StringComparison.OrdinalIgnoreCase)
            ? "TvShow"
            : "Movie";
        int? progress = item.ProgressPercentage is decimal value
            ? (int)Math.Round(value, MidpointRounding.AwayFromZero)
            : null;
        return new SocialProfileShelfItemResult(item.Id, item.Title, item.PosterUrl, contentType, progress);
    }

    private async Task<IReadOnlyList<SocialProfileShelfItemResult>> LoadLibraryTitlesAsync(
        Guid userId,
        bool includeWatched,
        bool includeFavorites,
        CancellationToken cancellationToken)
    {
        var ranked = new List<(DateTime At, SocialProfileShelfItemResult Item)>();

        if (includeWatched)
        {
            var watchedMovies = await (
                from watched in dbContext.WatchedMovies.AsNoTracking()
                join movie in dbContext.Movies.AsNoTracking() on watched.MovieId equals movie.Id
                where watched.UserId == userId
                orderby watched.WatchedAt descending
                select new { movie.Id, movie.Title, movie.PosterPath, watched.WatchedAt })
                .Take(40)
                .ToListAsync(cancellationToken);
            ranked.AddRange(watchedMovies.Select(row => (
                row.WatchedAt,
                new SocialProfileShelfItemResult(row.Id, row.Title, row.PosterPath, "Movie"))));

            var watchedShows = await (
                from watched in dbContext.WatchedEpisodes.AsNoTracking()
                join episode in dbContext.Episodes.AsNoTracking() on watched.EpisodeId equals episode.Id
                join season in dbContext.Seasons.AsNoTracking() on episode.SeasonId equals season.Id
                join show in dbContext.TvShows.AsNoTracking() on season.TvShowId equals show.Id
                where watched.UserId == userId
                orderby watched.WatchedAt descending
                select new { show.Id, show.Title, show.PosterPath, watched.WatchedAt })
                .Take(80)
                .ToListAsync(cancellationToken);
            ranked.AddRange(watchedShows.Select(row => (
                row.WatchedAt,
                new SocialProfileShelfItemResult(row.Id, row.Title, row.PosterPath, "TvShow"))));

            var watchlist = await (
                from item in dbContext.WatchlistItems.AsNoTracking()
                join list in dbContext.Watchlists.AsNoTracking() on item.WatchlistId equals list.Id
                where list.UserId == userId
                orderby item.CreatedAt descending
                select new { item.MovieId, item.TvShowId, item.CreatedAt })
                .Take(40)
                .ToListAsync(cancellationToken);
            var watchlistMovieIds = watchlist.Where(item => item.MovieId != null).Select(item => item.MovieId!.Value).ToArray();
            var watchlistShowIds = watchlist.Where(item => item.TvShowId != null).Select(item => item.TvShowId!.Value).ToArray();
            var watchlistMovies = await dbContext.Movies.AsNoTracking()
                .Where(movie => watchlistMovieIds.Contains(movie.Id))
                .Select(movie => new { movie.Id, movie.Title, movie.PosterPath })
                .ToListAsync(cancellationToken);
            var watchlistShows = await dbContext.TvShows.AsNoTracking()
                .Where(show => watchlistShowIds.Contains(show.Id))
                .Select(show => new { show.Id, show.Title, show.PosterPath })
                .ToListAsync(cancellationToken);
            foreach (var item in watchlist)
            {
                if (item.MovieId is Guid movieId)
                {
                    var movie = watchlistMovies.FirstOrDefault(row => row.Id == movieId);
                    if (movie is not null)
                    {
                        ranked.Add((item.CreatedAt, new SocialProfileShelfItemResult(movie.Id, movie.Title, movie.PosterPath, "Movie")));
                    }
                }
                else if (item.TvShowId is Guid showId)
                {
                    var show = watchlistShows.FirstOrDefault(row => row.Id == showId);
                    if (show is not null)
                    {
                        ranked.Add((item.CreatedAt, new SocialProfileShelfItemResult(show.Id, show.Title, show.PosterPath, "TvShow")));
                    }
                }
            }
        }

        if (includeFavorites)
        {
            var favoriteMovies = await (
                from favorite in dbContext.Favorites.AsNoTracking()
                join movie in dbContext.Movies.AsNoTracking() on favorite.MovieId equals movie.Id
                where favorite.UserId == userId
                orderby favorite.CreatedAt descending
                select new { movie.Id, movie.Title, movie.PosterPath, favorite.CreatedAt })
                .Take(40)
                .ToListAsync(cancellationToken);
            ranked.AddRange(favoriteMovies.Select(row => (
                row.CreatedAt,
                new SocialProfileShelfItemResult(row.Id, row.Title, row.PosterPath, "Movie"))));
            var favoriteShows = await (
                from favorite in dbContext.Favorites.AsNoTracking()
                join show in dbContext.TvShows.AsNoTracking() on favorite.TvShowId equals show.Id
                where favorite.UserId == userId
                orderby favorite.CreatedAt descending
                select new { show.Id, show.Title, show.PosterPath, favorite.CreatedAt })
                .Take(40)
                .ToListAsync(cancellationToken);
            ranked.AddRange(favoriteShows.Select(row => (
                row.CreatedAt,
                new SocialProfileShelfItemResult(row.Id, row.Title, row.PosterPath, "TvShow"))));
        }

        var seen = new HashSet<Guid>();
        var titles = new List<SocialProfileShelfItemResult>();
        foreach (var row in ranked.OrderByDescending(row => row.At))
        {
            if (!seen.Add(row.Item.Id)) continue;
            titles.Add(row.Item);
            if (titles.Count == 60) break;
        }

        return titles;
    }

    public async Task<bool> CanViewReviewsAsync(Guid userId, CancellationToken cancellationToken = default)
    {
        var profile = await GetProfileAsync(userId, cancellationToken);
        if (profile.FollowStatus == "Self")
        {
            return true;
        }

        if (profile.IsPrivate && profile.FollowStatus is not "Accepted")
        {
            return false;
        }

        var showReviews = await dbContext.UserPrivacySettings.AsNoTracking()
            .Where(settings => settings.UserId == userId)
            .Select(settings => (bool?)settings.ShowReviews)
            .FirstOrDefaultAsync(cancellationToken)
            ?? true;
        return showReviews;
    }

    public async Task<IReadOnlyList<WatchlistSummaryResult>> GetWatchlistsAsync(
        Guid userId, CancellationToken cancellationToken = default)
    {
        if (!await CanViewWatchlistsAsync(userId, cancellationToken))
        {
            return [];
        }

        var watchlists = await watchlistRepository.GetUserWatchlistsAsync(userId, cancellationToken);
        var watchlistIds = watchlists.Select(watchlist => watchlist.Id).ToArray();
        var itemCounts = await watchlistItemRepository.GetItemCountsByWatchlistIdsAsync(watchlistIds, cancellationToken);
        var coverPosterPaths = await watchlistItemRepository.GetCoverPosterPathsByWatchlistIdsAsync(
            watchlistIds,
            WatchlistCoverPosters.MaxPosters,
            cancellationToken);

        return watchlists
            .Select(watchlist => WatchlistMapper.ToSummaryResult(
                watchlist,
                itemCounts.GetValueOrDefault(watchlist.Id),
                coverPosterPaths.GetValueOrDefault(watchlist.Id)))
            .ToList();
    }

    public async Task<WatchlistItemsResult> GetWatchlistItemsAsync(
        Guid userId,
        Guid watchlistId,
        SearchContentType mediaType,
        WatchlistItemsSort sort,
        int page,
        int pageSize,
        CancellationToken cancellationToken = default)
    {
        var validationResult = SearchPaginationValidator.Validate(page, pageSize);
        if (!validationResult.IsValid)
        {
            throw new ValidationException(validationResult.ErrorMessage!);
        }

        if (!await CanViewWatchlistsAsync(userId, cancellationToken))
        {
            throw new NotFoundException("The requested watchlist was not found.");
        }

        if (await watchlistRepository.GetByIdForUserAsync(userId, watchlistId, cancellationToken) is null)
        {
            throw new NotFoundException("The requested watchlist was not found.");
        }

        var (items, totalCount) = await watchlistItemRepository.GetItemsAsync(
            watchlistId,
            mediaType,
            sort,
            page,
            pageSize,
            cancellationToken);

        return WatchlistMapper.ToItemsResult(items, page, pageSize, totalCount);
    }

    /// <summary>
    /// Watchlists follow the profile library rules: the owner always sees them, others need
    /// access to a public-or-followed profile and ShowWatched enabled.
    /// </summary>
    private async Task<bool> CanViewWatchlistsAsync(Guid userId, CancellationToken cancellationToken)
    {
        var profile = await GetProfileAsync(userId, cancellationToken);
        if (profile.FollowStatus == "Self")
        {
            return true;
        }

        if (profile.IsPrivate && profile.FollowStatus is not "Accepted")
        {
            return false;
        }

        return await dbContext.UserPrivacySettings.AsNoTracking()
            .Where(settings => settings.UserId == userId)
            .Select(settings => (bool?)settings.ShowWatched)
            .FirstOrDefaultAsync(cancellationToken)
            ?? true;
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
            .Select(item => new
            {
                item.Id,
                item.UserName,
                item.DisplayName,
                item.CreatedAt,
                item.ProfileCoverPosterPath,
                item.CustomProfileCoverStorageKey,
                item.Bio,
                item.SocialX,
                item.SocialInstagram,
                item.SocialSnapchat,
                item.SocialTiktok,
                item.SocialYoutube,
                item.SocialTwitch,
                item.SocialThreads,
                item.SocialWebsite,
            })
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
        var followsViewer = false;
        if (viewerId.HasValue && !isSelf)
        {
            var status = await dbContext.UserFollows.AsNoTracking()
                .Where(follow => follow.FollowerId == viewerId.Value && follow.FollowedUserId == userId)
                .Select(follow => (UserFollowStatus?)follow.Status)
                .FirstOrDefaultAsync(cancellationToken);
            followStatus = status?.ToString() ?? "None";
            followsViewer = await dbContext.UserFollows.AsNoTracking().AnyAsync(
                follow => follow.FollowerId == userId
                    && follow.FollowedUserId == viewerId.Value
                    && follow.Status == UserFollowStatus.Accepted, cancellationToken);
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
        var coverImageUrl = UserAvatarUrlBuilder.BuildPublicUrl(
            avatarStorageOptions.Value.PublicBaseUrl,
            user.CustomProfileCoverStorageKey);

        return new SocialProfileResult(
            user.Id,
            user.UserName,
            user.DisplayName,
            user.CreatedAt,
            avatar.EffectiveAvatarUrl,
            isPrivate,
            isSelf ? "Self" : followStatus,
            followersCount,
            followingCount,
            user.ProfileCoverPosterPath,
            coverImageUrl,
            user.Bio,
            ProfileSocialLinksResult.FromUser(
                user.SocialX,
                user.SocialInstagram,
                user.SocialSnapchat,
                user.SocialTiktok,
                user.SocialYoutube,
                user.SocialTwitch,
                user.SocialThreads,
                user.SocialWebsite),
            followsViewer);
    }

    public async Task SetProfileCoverPosterAsync(string? posterPath, CancellationToken cancellationToken = default)
    {
        if (!currentUser.IsAuthenticated || currentUser.UserId is not Guid userId)
            throw new AuthenticationException("Authentication is required.");
        var user = await dbContext.Users.FirstOrDefaultAsync(item => item.Id == userId && item.IsActive, cancellationToken);
        if (user is null)
        {
            throw new NotFoundException("User not found.");
        }

        if (!string.IsNullOrWhiteSpace(posterPath) && posterPath.Trim().Length > 512)
        {
            throw new ValidationException("Poster path is too long.");
        }

        var previousCoverKey = user.CustomProfileCoverStorageKey;
        if (!string.IsNullOrWhiteSpace(previousCoverKey))
        {
            user.ClearCustomProfileCoverStorageKey(DateTime.UtcNow);
        }

        user.SetProfileCoverPosterPath(posterPath, DateTime.UtcNow);
        await dbContext.SaveChangesAsync(cancellationToken);
        if (!string.IsNullOrWhiteSpace(previousCoverKey))
            await userAvatarService.BestEffortDeleteCustomAvatarAsync(previousCoverKey, cancellationToken);
    }
}
