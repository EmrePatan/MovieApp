using Microsoft.EntityFrameworkCore;
using MovieApp.Application.Abstractions.Persistence;
using MovieApp.Application.Library;
using MovieApp.Application.Mapping;
using MovieApp.Application.Models.Library;
using MovieApp.Application.Models.Search;
using MovieApp.Application.Common;
using MovieApp.Application.Services.WatchHistory;
using MovieApp.Infrastructure.Persistence.Search;

namespace MovieApp.Infrastructure.Persistence.Repositories;

public sealed class LibraryRepository(ApplicationDbContext dbContext) : ILibraryRepository
{
    private Guid? cachedStartedShowsUserId;
    private IReadOnlyList<TvShowCompletionRow>? cachedStartedShows;

    private const string CollectionStatusWatching = "watching";
    private const string CollectionStatusWatched = "watched";
    private const string CollectionStatusLiked = "liked";
    private const string CollectionStatusWatchlist = "watchlist";

    public async Task<(IReadOnlyList<LibraryItemResult> Items, int TotalCount)> GetWatchingAsync(
        Guid userId,
        SearchContentType mediaType,
        LibraryPageRequest request,
        CancellationToken cancellationToken = default)
    {
        if (mediaType == SearchContentType.Movie)
        {
            return ([], 0);
        }

        var inProgress = (await GetStartedShowsAsync(userId, cancellationToken))
            .Where(show => IsInProgress(show))
            .ToList();
        var shows = await LoadTvShowsAsync(
            await FilterShowIdsByTitleAsync(inProgress.Select(show => show.TvShowId), request, cancellationToken),
            cancellationToken);
        inProgress.RemoveAll(show => !shows.ContainsKey(show.TvShowId));

        var totalCount = ResolveCount(inProgress.Count, request);
        var rows = PageWatchingRows(inProgress, request);

        var nextEpisodesByShowId = await GetNextUnwatchedEpisodesAsync(
            userId,
            rows.Select(row => row.TvShowId).ToList(),
            cancellationToken);

        var items = rows
            .Select(row => MapTvShow(
                shows[row.TvShowId],
                CollectionStatusWatching,
                addedAt: null,
                watchedAt: null,
                lastActivityAt: row.LastWatchedAt,
                progressPercentage: WatchHistoryMapper.CalculateProgressPercentage(
                    row.RegularWatchedEpisodes,
                    row.RegularTotalEpisodes),
                nextEpisode: nextEpisodesByShowId.GetValueOrDefault(row.TvShowId),
                watchingSortInProgress: TvShowCompletionPolicy.IsWatchingLibrarySortInProgress(
                    row.RegularWatchedEpisodes,
                    row.RegularTotalEpisodes)))
            .ToList();

        return (items, totalCount);
    }

    public async Task<(IReadOnlyList<LibraryItemResult> Items, int TotalCount)> GetWatchedAsync(
        Guid userId,
        SearchContentType mediaType,
        LibraryPageRequest request,
        CancellationToken cancellationToken = default)
    {
        if (mediaType == SearchContentType.Movie)
        {
            return await GetWatchedMoviesAsync(userId, request, cancellationToken);
        }

        if (mediaType == SearchContentType.Tv)
        {
            return await GetWatchedTvShowsAsync(userId, request, cancellationToken);
        }

        var completedShows = await LoadCompletedShowRowsAsync(userId, request, cancellationToken);
        var movies = WatchedMovieRows(userId, ResolveWatchedMovieIdFilter(request));
        var totalCount = request.CountMode switch
        {
            LibraryCountMode.Skip => 0,
            LibraryCountMode.UseSnapshot => request.AfterCursor!.SnapshotTotalCount,
            _ => completedShows.Count + await movies.CountAsync(cancellationToken),
        };

        var pageRows = await MergeWatchedPageAsync(completedShows, movies, request, includeTypeTieBreak: true, cancellationToken);
        return (pageRows.Select(MapUnionRow).ToList(), totalCount);
    }

    public async Task<(IReadOnlyList<LibraryItemResult> Items, int TotalCount)> GetLikedAsync(
        Guid userId,
        SearchContentType mediaType,
        LibraryPageRequest request,
        CancellationToken cancellationToken = default)
    {
        var query = dbContext.Favorites
            .AsNoTracking()
            .Where(favorite => favorite.UserId == userId);

        query = ApplyFavoriteMediaTypeFilter(query, mediaType);

        if (!request.TitleMatch.IsEmpty)
        {
            query = ApplyFavoriteTitleFilter(query, request);
        }

        var totalCount = await ResolveTotalCountAsync(query, request, cancellationToken);

        var ordered = query
            .OrderByDescending(favorite => favorite.CreatedAt)
            .ThenBy(favorite => favorite.MovieId ?? favorite.TvShowId ?? favorite.Id);

        var likedCursor = request.AfterCursor;
        var likedAnchor = likedCursor?.GetSortInstant();
        IQueryable<Domain.Entities.Favorite> pagedFavorites = ordered;
        if (likedCursor is not null)
        {
            pagedFavorites = ordered.Where(favorite =>
                favorite.CreatedAt < likedAnchor
                || (favorite.CreatedAt == likedAnchor
                    && (favorite.MovieId ?? favorite.TvShowId ?? favorite.Id)
                        .CompareTo(likedCursor.PrimaryId) > 0));
        }
        else if (request.Page > 1)
        {
            pagedFavorites = ordered.Skip((request.Page - 1) * request.PageSize);
        }

        var pageKeys = await pagedFavorites
            .Select(favorite => new LikedPageKey(
                favorite.MovieId,
                favorite.TvShowId,
                favorite.CreatedAt,
                favorite.Id))
            .Take(request.FetchLimit)
            .ToListAsync(cancellationToken);

        var items = await MaterializeLikedPageAsync(pageKeys, cancellationToken);

        return (items, totalCount);
    }

    public async Task<(IReadOnlyList<LibraryItemResult> Items, int TotalCount)> GetWatchlistAsync(
        Guid userId,
        SearchContentType mediaType,
        LibraryPageRequest request,
        CancellationToken cancellationToken = default)
    {
        var query = dbContext.WatchlistItems
            .AsNoTracking()
            .Where(item => item.Watchlist.UserId == userId);

        if (mediaType == SearchContentType.Movie)
        {
            query = query.Where(item => item.MovieId != null);
        }
        else         if (mediaType == SearchContentType.Tv)
        {
            query = query.Where(item => item.TvShowId != null);
        }

        if (!request.TitleMatch.IsEmpty)
        {
            query = ApplyWatchlistItemTitleFilter(query, request);
        }

        var dedupedQuery = query
            .GroupBy(item => new
            {
                Type = item.MovieId != null ? "movie" : "tv",
                Id = item.MovieId ?? item.TvShowId!.Value
            })
            .Select(group => new
            {
                group.Key.Type,
                group.Key.Id,
                AddedAt = group.Max(item => item.CreatedAt)
            })
            .OrderByDescending(item => item.AddedAt)
            .ThenBy(item => item.Type)
            .ThenBy(item => item.Id);

        var totalCount = await ResolveTotalCountAsync(dedupedQuery, request, cancellationToken);

        var watchlistCursor = request.AfterCursor;
        var watchlistAnchor = watchlistCursor?.GetSortInstant();
        var pageKeys = watchlistCursor is not null
            ? await dedupedQuery
                .Where(item =>
                    item.AddedAt < watchlistAnchor
                    || (item.AddedAt == watchlistAnchor && item.Type.CompareTo(watchlistCursor.Type) > 0)
                    || (item.AddedAt == watchlistAnchor
                        && item.Type == watchlistCursor.Type
                        && item.Id.CompareTo(watchlistCursor.PrimaryId) > 0))
                .Take(request.FetchLimit)
                .ToListAsync(cancellationToken)
            : request.Page > 1
                ? await dedupedQuery
                    .Skip((request.Page - 1) * request.PageSize)
                    .Take(request.FetchLimit)
                    .ToListAsync(cancellationToken)
                : await dedupedQuery
                    .Take(request.FetchLimit)
                    .ToListAsync(cancellationToken);

        var movieIds = pageKeys
            .Where(item => item.Type == "movie")
            .Select(item => item.Id)
            .ToList();
        var tvShowIds = pageKeys
            .Where(item => item.Type == "tv")
            .Select(item => item.Id)
            .ToList();

        var movies = await dbContext.Movies
            .AsNoTracking()
            .Where(movie => movieIds.Contains(movie.Id))
            .ToDictionaryAsync(movie => movie.Id, cancellationToken);

        var tvShows = await dbContext.TvShows
            .AsNoTracking()
            .Where(tvShow => tvShowIds.Contains(tvShow.Id))
            .ToDictionaryAsync(tvShow => tvShow.Id, cancellationToken);

        var items = pageKeys
            .Select(key =>
            {
                if (key.Type == "movie" && movies.TryGetValue(key.Id, out var movie))
                {
                    return MapMovie(
                        movie,
                        CollectionStatusWatchlist,
                        addedAt: key.AddedAt,
                        watchedAt: null,
                        lastActivityAt: key.AddedAt,
                        progressPercentage: null,
                        nextEpisode: null);
                }

                if (key.Type == "tv" && tvShows.TryGetValue(key.Id, out var tvShow))
                {
                    return MapTvShow(
                        tvShow,
                        CollectionStatusWatchlist,
                        addedAt: key.AddedAt,
                        watchedAt: null,
                        lastActivityAt: key.AddedAt,
                        progressPercentage: null,
                        nextEpisode: null);
                }

                return null;
            })
            .Where(item => item is not null)
            .Cast<LibraryItemResult>()
            .ToList();

        return (items, totalCount);
    }

    private async Task<(IReadOnlyList<LibraryItemResult> Items, int TotalCount)> GetWatchedMoviesAsync(
        Guid userId,
        LibraryPageRequest request,
        CancellationToken cancellationToken)
    {
        var rows = WatchedMovieRows(userId, ResolveWatchedMovieIdFilter(request));
        var totalCount = await ResolveTotalCountAsync(rows, request, cancellationToken);
        var pageRows = await PageWatchedRows(rows, request, includeTypeTieBreak: false)
            .Take(request.FetchLimit)
            .ToListAsync(cancellationToken);

        return (pageRows.Select(MapUnionRow).ToList(), totalCount);
    }

    private async Task<(IReadOnlyList<LibraryItemResult> Items, int TotalCount)> GetWatchedTvShowsAsync(
        Guid userId,
        LibraryPageRequest request,
        CancellationToken cancellationToken)
    {
        var rows = await LoadCompletedShowRowsAsync(userId, request, cancellationToken);
        var totalCount = ResolveCount(rows.Count, request);
        var pageRows = PageWatchedRowsInMemory(rows, request, includeTypeTieBreak: false);
        return (pageRows.Select(MapUnionRow).ToList(), totalCount);
    }

    private IQueryable<Guid>? ResolveWatchedMovieIdFilter(LibraryPageRequest? request)
    {
        if (request is null || request.TitleMatch.IsEmpty)
        {
            return null;
        }

        return LibrarySearchTitleFilter.MatchingMovieIds(
            dbContext,
            request.TitleMatch,
            request.TitleMatchContentLocale);
    }

    private IQueryable<WatchedUnionRow> WatchedMovieRows(
        Guid userId,
        IQueryable<Guid>? restrictToMovieIds = null)
    {
        var query = dbContext.WatchedMovies
            .AsNoTracking()
            .Where(watchedMovie => watchedMovie.UserId == userId);

        if (restrictToMovieIds is not null)
        {
            query = query.Where(watchedMovie => restrictToMovieIds.Contains(watchedMovie.MovieId));
        }

        return query.Select(watchedMovie => new WatchedUnionRow
            {
                Id = watchedMovie.MovieId,
                Type = "movie",
                Title = watchedMovie.Movie.Title,
                OriginalTitle = watchedMovie.Movie.OriginalTitle,
                PosterUrl = watchedMovie.Movie.PosterPath,
                BackdropUrl = watchedMovie.Movie.BackdropPath,
                ReleaseDate = watchedMovie.Movie.ReleaseDate,
                FirstAirDate = null,
                VoteAverage = watchedMovie.Movie.VoteAverage,
                WatchedAt = watchedMovie.WatchedAt,
                LastActivityAt = watchedMovie.WatchedAt
            });
    }

    private async Task<IReadOnlyList<TvShowCompletionRow>> GetStartedShowsAsync(
        Guid userId,
        CancellationToken cancellationToken)
    {
        if (cachedStartedShows is not null && cachedStartedShowsUserId == userId)
        {
            return cachedStartedShows;
        }

        var rows = await TvShowCompletionQueries.LoadStartedShowsAsync(dbContext, userId, cancellationToken);
        cachedStartedShows = rows;
        cachedStartedShowsUserId = userId;
        return rows;
    }

    private async Task<List<WatchedUnionRow>> LoadCompletedShowRowsAsync(
        Guid userId,
        LibraryPageRequest request,
        CancellationToken cancellationToken)
    {
        var completed = (await GetStartedShowsAsync(userId, cancellationToken))
            .Where(show => IsCompleted(show) && show.LastWatchedAt is not null)
            .ToList();
        var ids = await FilterShowIdsByTitleAsync(completed.Select(show => show.TvShowId), request, cancellationToken);
        var shows = await LoadTvShowsAsync(ids, cancellationToken);

        return completed
            .Where(show => shows.ContainsKey(show.TvShowId))
            .Select(show =>
            {
                var tvShow = shows[show.TvShowId];
                return new WatchedUnionRow
                {
                    Id = tvShow.Id,
                    Type = "tv",
                    Title = tvShow.Title,
                    OriginalTitle = tvShow.OriginalTitle,
                    PosterUrl = tvShow.PosterPath,
                    BackdropUrl = tvShow.BackdropPath,
                    ReleaseDate = null,
                    FirstAirDate = tvShow.FirstAirDate,
                    VoteAverage = tvShow.VoteAverage,
                    WatchedAt = show.LastWatchedAt,
                    LastActivityAt = show.LastWatchedAt!.Value
                };
            })
            .OrderBy(row => row, WatchedRowComparer.WithoutType)
            .ToList();
    }

    private async Task<List<Guid>> FilterShowIdsByTitleAsync(
        IEnumerable<Guid> showIds,
        LibraryPageRequest request,
        CancellationToken cancellationToken)
    {
        var ids = showIds.Distinct().ToList();
        if (ids.Count == 0 || request.TitleMatch.IsEmpty)
        {
            return ids;
        }

        return await LibrarySearchTitleFilter.WhereTvShowMatches(
                dbContext,
                dbContext.TvShows.AsNoTracking().Where(tvShow => ids.Contains(tvShow.Id)),
                request.TitleMatch,
                request.TitleMatchContentLocale)
            .Select(tvShow => tvShow.Id)
            .ToListAsync(cancellationToken);
    }

    private async Task<Dictionary<Guid, Domain.Entities.TvShow>> LoadTvShowsAsync(
        List<Guid> tvShowIds,
        CancellationToken cancellationToken)
    {
        if (tvShowIds.Count == 0)
        {
            return [];
        }

        return await dbContext.TvShows
            .AsNoTracking()
            .Where(tvShow => tvShowIds.Contains(tvShow.Id))
            .ToDictionaryAsync(tvShow => tvShow.Id, cancellationToken);
    }

    private static List<TvShowCompletionRow> PageWatchingRows(
        IReadOnlyList<TvShowCompletionRow> rows,
        LibraryPageRequest request)
    {
        IEnumerable<TvShowCompletionRow> ordered = rows
            .OrderByDescending(row => row.RegularWatchedEpisodes < row.RegularTotalEpisodes)
            .ThenBy(row => row.LastWatchedAt, PostgresTimestampDescComparer.Instance)
            .ThenBy(row => row.TvShowId, PostgresGuidComparer.Instance);

        if (request.AfterCursor is { } cursor)
        {
            var anchor = cursor.GetSortInstant();
            ordered = ordered.Where(row => IsAfterWatchingCursor(row, cursor, anchor));
        }
        else if (request.Page > 1)
        {
            ordered = ordered.Skip(GetPageSkip(request.Page, request.PageSize));
        }

        return ordered.Take(request.FetchLimit).ToList();
    }

    private static bool IsAfterWatchingCursor(
        TvShowCompletionRow row,
        LibraryKeysetCursor cursor,
        DateTime anchor)
    {
        var inProgress = row.RegularWatchedEpisodes < row.RegularTotalEpisodes;
        if (cursor.WatchingInProgress && !inProgress)
        {
            return true;
        }

        if (inProgress != cursor.WatchingInProgress || row.LastWatchedAt is not DateTime lastWatchedAt)
        {
            return false;
        }

        if (lastWatchedAt < anchor)
        {
            return true;
        }

        return lastWatchedAt == anchor
            && PostgresGuidComparer.Instance.Compare(row.TvShowId, cursor.PrimaryId) > 0;
    }

    private static async Task<List<WatchedUnionRow>> MergeWatchedPageAsync(
        IReadOnlyList<WatchedUnionRow> completedShows,
        IQueryable<WatchedUnionRow> movies,
        LibraryPageRequest request,
        bool includeTypeTieBreak,
        CancellationToken cancellationToken)
    {
        var skip = request.AfterCursor is null ? GetPageSkip(request.Page, request.PageSize) : 0;
        var movieLimitLong = (long)skip + request.FetchLimit;
        var movieLimit = movieLimitLong > int.MaxValue ? int.MaxValue : (int)movieLimitLong;
        IQueryable<WatchedUnionRow> orderedMovies = OrderWatchedRows(movies, includeTypeTieBreak);
        if (request.AfterCursor is { } cursor)
        {
            var anchor = cursor.GetSortInstant();
            orderedMovies = includeTypeTieBreak
                ? orderedMovies.Where(row =>
                    row.LastActivityAt < anchor
                    || (row.LastActivityAt == anchor && row.Type.CompareTo(cursor.Type) > 0)
                    || (row.LastActivityAt == anchor
                        && row.Type == cursor.Type
                        && row.Id.CompareTo(cursor.PrimaryId) > 0))
                : orderedMovies.Where(row =>
                    row.LastActivityAt < anchor
                    || (row.LastActivityAt == anchor && row.Id.CompareTo(cursor.PrimaryId) > 0));
        }

        var movieRows = await orderedMovies
            .Take(movieLimit)
            .ToListAsync(cancellationToken);

        IEnumerable<WatchedUnionRow> shows = completedShows;
        if (request.AfterCursor is not null)
        {
            shows = completedShows.Where(row => IsAfterWatchedCursor(row, request.AfterCursor, includeTypeTieBreak));
        }

        var merged = MergeSorted(shows, movieRows, includeTypeTieBreak);
        if (request.AfterCursor is null && request.Page > 1)
        {
            merged = merged.Skip(GetPageSkip(request.Page, request.PageSize)).ToList();
        }

        return merged.Take(request.FetchLimit).ToList();
    }

    private static IOrderedQueryable<WatchedUnionRow> OrderWatchedRows(
        IQueryable<WatchedUnionRow> rows,
        bool includeTypeTieBreak) =>
        includeTypeTieBreak
            ? rows
                .OrderByDescending(row => row.LastActivityAt)
                .ThenBy(row => row.Type)
                .ThenBy(row => row.Id)
            : rows
                .OrderByDescending(row => row.LastActivityAt)
                .ThenBy(row => row.Id);

    private static List<WatchedUnionRow> MergeSorted(
        IEnumerable<WatchedUnionRow> shows,
        List<WatchedUnionRow> movies,
        bool includeTypeTieBreak)
    {
        var comparer = includeTypeTieBreak ? WatchedRowComparer.WithType : WatchedRowComparer.WithoutType;
        var merged = new List<WatchedUnionRow>();
        using var showEnumerator = shows.GetEnumerator();
        var hasShow = showEnumerator.MoveNext();
        var movieIndex = 0;
        while (hasShow && movieIndex < movies.Count)
        {
            if (comparer.Compare(showEnumerator.Current, movies[movieIndex]) <= 0)
            {
                merged.Add(showEnumerator.Current);
                hasShow = showEnumerator.MoveNext();
            }
            else
            {
                merged.Add(movies[movieIndex]);
                movieIndex++;
            }
        }

        while (hasShow)
        {
            merged.Add(showEnumerator.Current);
            hasShow = showEnumerator.MoveNext();
        }

        while (movieIndex < movies.Count)
        {
            merged.Add(movies[movieIndex]);
            movieIndex++;
        }

        return merged;
    }

    private static List<WatchedUnionRow> PageWatchedRowsInMemory(
        IReadOnlyList<WatchedUnionRow> rows,
        LibraryPageRequest request,
        bool includeTypeTieBreak)
    {
        IEnumerable<WatchedUnionRow> ordered = rows;
        if (request.AfterCursor is { } cursor)
        {
            ordered = ordered.Where(row => IsAfterWatchedCursor(row, cursor, includeTypeTieBreak));
        }
        else if (request.Page > 1)
        {
            ordered = ordered.Skip(GetPageSkip(request.Page, request.PageSize));
        }

        return ordered.Take(request.FetchLimit).ToList();
    }

    private static bool IsAfterWatchedCursor(
        WatchedUnionRow row,
        LibraryKeysetCursor cursor,
        bool includeTypeTieBreak)
    {
        var anchor = cursor.GetSortInstant();
        if (row.LastActivityAt < anchor)
        {
            return true;
        }

        if (row.LastActivityAt != anchor)
        {
            return false;
        }

        if (includeTypeTieBreak)
        {
            var typeCompare = string.CompareOrdinal(row.Type, cursor.Type);
            if (typeCompare > 0)
            {
                return true;
            }

            if (typeCompare < 0)
            {
                return false;
            }
        }

        return PostgresGuidComparer.Instance.Compare(row.Id, cursor.PrimaryId) > 0;
    }

    private static int ResolveCount(int count, LibraryPageRequest request) =>
        request.CountMode switch
        {
            LibraryCountMode.Skip => 0,
            LibraryCountMode.UseSnapshot => request.AfterCursor!.SnapshotTotalCount,
            _ => count,
        };

    private static bool IsInProgress(TvShowCompletionRow row) =>
        !(row.IsConcluded &&
          row.RegularTotalEpisodes > 0 &&
          row.RegularWatchedEpisodes >= row.RegularTotalEpisodes);

    private static bool IsCompleted(TvShowCompletionRow row) =>
        row.IsConcluded &&
        row.RegularTotalEpisodes > 0 &&
        row.RegularWatchedEpisodes >= row.RegularTotalEpisodes;

    private static async Task<int> ResolveTotalCountAsync<T>(
        IQueryable<T> query,
        LibraryPageRequest request,
        CancellationToken cancellationToken) =>
        request.CountMode switch
        {
            LibraryCountMode.Skip => 0,
            LibraryCountMode.UseSnapshot => request.AfterCursor!.SnapshotTotalCount,
            _ => await query.CountAsync(cancellationToken),
        };

    private async Task<IReadOnlyList<LibraryItemResult>> MaterializeLikedPageAsync(
        IReadOnlyList<LikedPageKey> pageKeys,
        CancellationToken cancellationToken)
    {
        if (pageKeys.Count == 0)
        {
            return [];
        }

        var movieIds = pageKeys
            .Where(key => key.MovieId.HasValue)
            .Select(key => key.MovieId!.Value)
            .ToList();
        var tvShowIds = pageKeys
            .Where(key => key.TvShowId.HasValue)
            .Select(key => key.TvShowId!.Value)
            .ToList();

        var movies = await dbContext.Movies
            .AsNoTracking()
            .Where(movie => movieIds.Contains(movie.Id))
            .ToDictionaryAsync(movie => movie.Id, cancellationToken);

        var tvShows = await dbContext.TvShows
            .AsNoTracking()
            .Where(tvShow => tvShowIds.Contains(tvShow.Id))
            .ToDictionaryAsync(tvShow => tvShow.Id, cancellationToken);

        return pageKeys
            .Select(key =>
            {
                if (key.MovieId.HasValue && movies.TryGetValue(key.MovieId.Value, out var movie))
                {
                    return MapMovie(
                        movie,
                        CollectionStatusLiked,
                        addedAt: key.CreatedAt,
                        watchedAt: null,
                        lastActivityAt: key.CreatedAt,
                        progressPercentage: null,
                        nextEpisode: null);
                }

                if (key.TvShowId.HasValue && tvShows.TryGetValue(key.TvShowId.Value, out var tvShow))
                {
                    return MapTvShow(
                        tvShow,
                        CollectionStatusLiked,
                        addedAt: key.CreatedAt,
                        watchedAt: null,
                        lastActivityAt: key.CreatedAt,
                        progressPercentage: null,
                        nextEpisode: null);
                }

                return null;
            })
            .Where(item => item is not null)
            .Cast<LibraryItemResult>()
            .ToList();
    }

    private static IQueryable<WatchedUnionRow> PageWatchedRows(
        IQueryable<WatchedUnionRow> rows,
        LibraryPageRequest request,
        bool includeTypeTieBreak)
    {
        IOrderedQueryable<WatchedUnionRow> ordered = includeTypeTieBreak
            ? rows
                .OrderByDescending(row => row.LastActivityAt)
                .ThenBy(row => row.Type)
                .ThenBy(row => row.Id)
            : rows
                .OrderByDescending(row => row.LastActivityAt)
                .ThenBy(row => row.Id);

        if (request.AfterCursor is not null)
        {
            var cursor = request.AfterCursor;
            var anchor = cursor.GetSortInstant();
            return includeTypeTieBreak
                ? ordered.Where(row =>
                    row.LastActivityAt < anchor
                    || (row.LastActivityAt == anchor && row.Type.CompareTo(cursor.Type) > 0)
                    || (row.LastActivityAt == anchor
                        && row.Type == cursor.Type
                        && row.Id.CompareTo(cursor.PrimaryId) > 0))
                : ordered.Where(row =>
                    row.LastActivityAt < anchor
                    || (row.LastActivityAt == anchor && row.Id.CompareTo(cursor.PrimaryId) > 0));
        }

        if (request.Page > 1)
        {
            return ordered.Skip(GetPageSkip(request.Page, request.PageSize));
        }

        return ordered;
    }

    private static int GetPageSkip(int page, int pageSize)
    {
        var skip = (long)(page - 1) * pageSize;
        return skip > int.MaxValue ? int.MaxValue : (int)skip;
    }

    private async Task<Dictionary<Guid, LibraryNextEpisodeResult>> GetNextUnwatchedEpisodesAsync(
        Guid userId,
        List<Guid> tvShowIds,
        CancellationToken cancellationToken)
    {
        if (tvShowIds.Count == 0)
        {
            return [];
        }

        var nextEpisodes = await dbContext.Episodes
            .AsNoTracking()
            .Where(episode =>
                tvShowIds.Contains(episode.Season.TvShowId) &&
                episode.Season.SeasonNumber >= 1)
            .Where(episode => !dbContext.WatchedEpisodes.Any(watchedEpisode =>
                watchedEpisode.UserId == userId &&
                watchedEpisode.EpisodeId == episode.Id))
            .GroupBy(episode => episode.Season.TvShowId)
            .Select(group => group
                .OrderBy(episode => episode.Season.SeasonNumber)
                .ThenBy(episode => episode.EpisodeNumber)
                .Select(episode => new
                {
                    episode.Season.TvShowId,
                    episode.Id,
                    episode.Season.SeasonNumber,
                    episode.EpisodeNumber,
                    episode.Name
                })
                .First())
            .ToListAsync(cancellationToken);

        return nextEpisodes.ToDictionary(
            episode => episode.TvShowId,
            episode => new LibraryNextEpisodeResult(
                episode.Id,
                episode.SeasonNumber,
                episode.EpisodeNumber,
                episode.Name));
    }

    private IQueryable<Domain.Entities.WatchlistItem> ApplyWatchlistItemTitleFilter(
        IQueryable<Domain.Entities.WatchlistItem> query,
        LibraryPageRequest request)
    {
        var matchingMovies = LibrarySearchTitleFilter.MatchingMovieIds(
            dbContext,
            request.TitleMatch,
            request.TitleMatchContentLocale);
        var matchingTvShows = LibrarySearchTitleFilter.MatchingTvShowIds(
            dbContext,
            request.TitleMatch,
            request.TitleMatchContentLocale);

        return query.Where(item =>
            (item.MovieId != null && matchingMovies.Contains(item.MovieId.Value))
            || (item.TvShowId != null && matchingTvShows.Contains(item.TvShowId.Value)));
    }

    private IQueryable<Domain.Entities.Favorite> ApplyFavoriteTitleFilter(
        IQueryable<Domain.Entities.Favorite> query,
        LibraryPageRequest request)
    {
        var matchingMovies = LibrarySearchTitleFilter.MatchingMovieIds(
            dbContext,
            request.TitleMatch,
            request.TitleMatchContentLocale);
        var matchingTvShows = LibrarySearchTitleFilter.MatchingTvShowIds(
            dbContext,
            request.TitleMatch,
            request.TitleMatchContentLocale);

        return query.Where(favorite =>
            (favorite.MovieId != null && matchingMovies.Contains(favorite.MovieId.Value))
            || (favorite.TvShowId != null && matchingTvShows.Contains(favorite.TvShowId.Value)));
    }

    private static IQueryable<Domain.Entities.Favorite> ApplyFavoriteMediaTypeFilter(
        IQueryable<Domain.Entities.Favorite> query,
        SearchContentType mediaType) =>
        mediaType switch
        {
            SearchContentType.Movie => query.Where(favorite => favorite.MovieId != null),
            SearchContentType.Tv => query.Where(favorite => favorite.TvShowId != null),
            _ => query
        };

    private static LibraryItemResult MapUnionRow(WatchedUnionRow row) =>
        row.Type == "movie"
            ? new LibraryItemResult(
                row.Id,
                "movie",
                row.Title,
                row.OriginalTitle,
                row.PosterUrl,
                row.BackdropUrl,
                row.ReleaseDate?.Year,
                row.VoteAverage,
                null,
                row.WatchedAt,
                row.LastActivityAt,
                null,
                null,
                CollectionStatusWatched)
            : new LibraryItemResult(
                row.Id,
                "tv",
                row.Title,
                row.OriginalTitle,
                row.PosterUrl,
                row.BackdropUrl,
                row.FirstAirDate?.Year,
                row.VoteAverage,
                null,
                row.WatchedAt,
                row.LastActivityAt,
                100m,
                null,
                CollectionStatusWatched);

    private static LibraryItemResult MapMovie(
        Domain.Entities.Movie movie,
        string collectionStatus,
        DateTime? addedAt,
        DateTime? watchedAt,
        DateTime? lastActivityAt,
        decimal? progressPercentage,
        LibraryNextEpisodeResult? nextEpisode) =>
        new(
            movie.Id,
            "movie",
            movie.Title,
            movie.OriginalTitle,
            movie.PosterPath,
            movie.BackdropPath,
            movie.ReleaseDate?.Year,
            movie.VoteAverage,
            addedAt,
            watchedAt,
            lastActivityAt,
            progressPercentage,
            nextEpisode,
            collectionStatus);

    private static LibraryItemResult MapTvShow(
        Domain.Entities.TvShow tvShow,
        string collectionStatus,
        DateTime? addedAt,
        DateTime? watchedAt,
        DateTime? lastActivityAt,
        decimal? progressPercentage,
        LibraryNextEpisodeResult? nextEpisode,
        bool? watchingSortInProgress = null) =>
        new(
            tvShow.Id,
            "tv",
            tvShow.Title,
            tvShow.OriginalTitle,
            tvShow.PosterPath,
            tvShow.BackdropPath,
            tvShow.FirstAirDate?.Year,
            tvShow.VoteAverage,
            addedAt,
            watchedAt,
            lastActivityAt,
            progressPercentage,
            nextEpisode,
            collectionStatus,
            watchingSortInProgress);

    internal sealed class WatchedUnionRow
    {
        public Guid Id { get; init; }

        public string Type { get; init; } = string.Empty;

        public string Title { get; init; } = string.Empty;

        public string? OriginalTitle { get; init; }

        public string? PosterUrl { get; init; }

        public string? BackdropUrl { get; init; }

        public DateOnly? ReleaseDate { get; init; }

        public DateOnly? FirstAirDate { get; init; }

        public decimal VoteAverage { get; init; }

        public DateTime? WatchedAt { get; init; }

        public DateTime LastActivityAt { get; init; }
    }

    private sealed class WatchedRowComparer(bool includeTypeTieBreak) : IComparer<WatchedUnionRow>
    {
        internal static readonly WatchedRowComparer WithType = new(true);

        internal static readonly WatchedRowComparer WithoutType = new(false);

        public int Compare(WatchedUnionRow? x, WatchedUnionRow? y)
        {
            if (x is null || y is null)
            {
                return x is null ? (y is null ? 0 : -1) : 1;
            }

            var time = y.LastActivityAt.CompareTo(x.LastActivityAt);
            if (time != 0)
            {
                return time;
            }

            if (includeTypeTieBreak)
            {
                var type = string.CompareOrdinal(x.Type, y.Type);
                if (type != 0)
                {
                    return type;
                }
            }

            return PostgresGuidComparer.Instance.Compare(x.Id, y.Id);
        }
    }

    private sealed record LikedPageKey(
        Guid? MovieId,
        Guid? TvShowId,
        DateTime CreatedAt,
        Guid FavoriteId);
}
