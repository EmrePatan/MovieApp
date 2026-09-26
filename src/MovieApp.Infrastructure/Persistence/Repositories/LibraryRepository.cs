using Microsoft.EntityFrameworkCore;
using MovieApp.Application.Abstractions.Persistence;
using MovieApp.Application.Library;
using MovieApp.Application.Mapping;
using MovieApp.Application.Models.Library;
using MovieApp.Application.Models.Search;

namespace MovieApp.Infrastructure.Persistence.Repositories;

public sealed class LibraryRepository(ApplicationDbContext dbContext) : ILibraryRepository
{
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

        var query = TvShowCompletionQueries.StartedShows(dbContext, userId)
            .Where(TvShowCompletionQueries.IsInProgress)
            .Join(
                dbContext.TvShows.AsNoTracking(),
                show => show.TvShowId,
                tvShow => tvShow.Id,
                (show, tvShow) => new
                {
                    show.LastWatchedAt,
                    show.RegularTotalEpisodes,
                    show.RegularWatchedEpisodes,
                    TvShow = tvShow
                });

        var totalCount = request.ExecuteCount
            ? await query.CountAsync(cancellationToken)
            : request.AfterCursor!.SnapshotTotalCount;

        var ordered = query
            .OrderByDescending(item => item.RegularWatchedEpisodes < item.RegularTotalEpisodes)
            .ThenByDescending(item => item.LastWatchedAt)
            .ThenBy(item => item.TvShow.Id);

        var watchingCursor = request.AfterCursor;
        var watchingAnchorInstant = watchingCursor?.GetSortInstant();
        var rows = watchingCursor is not null
            ? await ordered
                .Where(item =>
                    (watchingCursor.WatchingInProgress
                        && item.RegularWatchedEpisodes >= item.RegularTotalEpisodes)
                    || ((item.RegularWatchedEpisodes < item.RegularTotalEpisodes) == watchingCursor.WatchingInProgress
                        && item.LastWatchedAt < watchingAnchorInstant)
                    || ((item.RegularWatchedEpisodes < item.RegularTotalEpisodes) == watchingCursor.WatchingInProgress
                        && item.LastWatchedAt == watchingAnchorInstant
                        && item.TvShow.Id.CompareTo(watchingCursor.PrimaryId) > 0))
                .Take(request.FetchLimit)
                .ToListAsync(cancellationToken)
            : request.Page > 1
                ? await ordered
                    .Skip((request.Page - 1) * request.PageSize)
                    .Take(request.FetchLimit)
                    .ToListAsync(cancellationToken)
                : await ordered
                    .Take(request.FetchLimit)
                    .ToListAsync(cancellationToken);

        var nextEpisodesByShowId = await GetNextUnwatchedEpisodesAsync(
            userId,
            rows.Select(row => row.TvShow.Id).ToList(),
            cancellationToken);

        var items = rows
            .Select(row => MapTvShow(
                row.TvShow,
                CollectionStatusWatching,
                addedAt: null,
                watchedAt: null,
                lastActivityAt: row.LastWatchedAt,
                progressPercentage: WatchHistoryMapper.CalculateProgressPercentage(
                    row.RegularWatchedEpisodes,
                    row.RegularTotalEpisodes),
                nextEpisode: nextEpisodesByShowId.GetValueOrDefault(row.TvShow.Id)))
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

        var merged = WatchedUnionRows(userId);
        var totalCount = request.ExecuteCount
            ? await merged.CountAsync(cancellationToken)
            : request.AfterCursor!.SnapshotTotalCount;
        var pageRows = await PageWatchedRows(merged, request, includeTypeTieBreak: true)
            .Take(request.FetchLimit)
            .ToListAsync(cancellationToken);

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

        var totalCount = request.ExecuteCount
            ? await query.CountAsync(cancellationToken)
            : request.AfterCursor!.SnapshotTotalCount;

        var queryWithIncludes = query
            .Include(favorite => favorite.Movie)
            .Include(favorite => favorite.TvShow);

        var ordered = queryWithIncludes
            .OrderByDescending(favorite => favorite.CreatedAt)
            .ThenBy(favorite => favorite.MovieId ?? favorite.TvShowId ?? favorite.Id);

        var likedCursor = request.AfterCursor;
        var likedAnchor = likedCursor?.GetSortInstant();
        var favorites = likedCursor is not null
            ? await ordered
                .Where(favorite =>
                    favorite.CreatedAt < likedAnchor
                    || (favorite.CreatedAt == likedAnchor
                        && (favorite.MovieId ?? favorite.TvShowId ?? favorite.Id).CompareTo(likedCursor.PrimaryId) > 0))
                .Take(request.FetchLimit)
                .ToListAsync(cancellationToken)
            : request.Page > 1
                ? await ordered
                    .Skip((request.Page - 1) * request.PageSize)
                    .Take(request.FetchLimit)
                    .ToListAsync(cancellationToken)
                : await ordered
                    .Take(request.FetchLimit)
                    .ToListAsync(cancellationToken);

        var items = favorites
            .Select(favorite => favorite.Movie is not null
                ? MapMovie(
                    favorite.Movie,
                    CollectionStatusLiked,
                    addedAt: favorite.CreatedAt,
                    watchedAt: null,
                    lastActivityAt: favorite.CreatedAt,
                    progressPercentage: null,
                    nextEpisode: null)
                : MapTvShow(
                    favorite.TvShow!,
                    CollectionStatusLiked,
                    addedAt: favorite.CreatedAt,
                    watchedAt: null,
                    lastActivityAt: favorite.CreatedAt,
                    progressPercentage: null,
                    nextEpisode: null))
            .ToList();

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
        else if (mediaType == SearchContentType.Tv)
        {
            query = query.Where(item => item.TvShowId != null);
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

        var totalCount = request.ExecuteCount
            ? await dedupedQuery.CountAsync(cancellationToken)
            : request.AfterCursor!.SnapshotTotalCount;

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
        var query = dbContext.WatchedMovies
            .AsNoTracking()
            .Where(watchedMovie => watchedMovie.UserId == userId);

        var totalCount = request.ExecuteCount
            ? await query.CountAsync(cancellationToken)
            : request.AfterCursor!.SnapshotTotalCount;

        var ordered = query
            .OrderByDescending(watchedMovie => watchedMovie.WatchedAt)
            .ThenBy(watchedMovie => watchedMovie.MovieId);

        var watchedCursor = request.AfterCursor;
        var watchedAnchor = watchedCursor?.GetSortInstant();
        var watchedMovies = watchedCursor is not null
            ? await ordered
                .Where(watchedMovie =>
                    watchedMovie.WatchedAt < watchedAnchor
                    || (watchedMovie.WatchedAt == watchedAnchor
                        && watchedMovie.MovieId.CompareTo(watchedCursor.PrimaryId) > 0))
                .Include(watchedMovie => watchedMovie.Movie)
                .Take(request.FetchLimit)
                .ToListAsync(cancellationToken)
            : request.Page > 1
                ? await ordered
                    .Include(watchedMovie => watchedMovie.Movie)
                    .Skip((request.Page - 1) * request.PageSize)
                    .Take(request.FetchLimit)
                    .ToListAsync(cancellationToken)
                : await ordered
                    .Include(watchedMovie => watchedMovie.Movie)
                    .Take(request.FetchLimit)
                    .ToListAsync(cancellationToken);

        var items = watchedMovies
            .Select(watchedMovie => MapMovie(
                watchedMovie.Movie,
                CollectionStatusWatched,
                addedAt: null,
                watchedAt: watchedMovie.WatchedAt,
                lastActivityAt: watchedMovie.WatchedAt,
                progressPercentage: null,
                nextEpisode: null))
            .ToList();

        return (items, totalCount);
    }

    private async Task<(IReadOnlyList<LibraryItemResult> Items, int TotalCount)> GetWatchedTvShowsAsync(
        Guid userId,
        LibraryPageRequest request,
        CancellationToken cancellationToken)
    {
        var rows = CompletedTvShowRows(userId);
        var totalCount = request.ExecuteCount
            ? await rows.CountAsync(cancellationToken)
            : request.AfterCursor!.SnapshotTotalCount;
        var pageRows = await PageWatchedRows(rows, request, includeTypeTieBreak: false)
            .Take(request.FetchLimit)
            .ToListAsync(cancellationToken);

        return (pageRows.Select(MapUnionRow).ToList(), totalCount);
    }

    internal IQueryable<WatchedUnionRow> WatchedUnionRows(Guid userId) =>
        WatchedMovieRows(userId).Concat(CompletedTvShowRows(userId));

    internal IQueryable<WatchedUnionRow> CompletedTvShowRows(Guid userId) =>
        CompletedTvShows(userId)
            .Select(row => new WatchedUnionRow
            {
                Id = row.TvShow.Id,
                Type = "tv",
                Title = row.TvShow.Title,
                OriginalTitle = row.TvShow.OriginalTitle,
                PosterUrl = row.TvShow.PosterPath,
                BackdropUrl = row.TvShow.BackdropPath,
                ReleaseDate = null,
                FirstAirDate = row.TvShow.FirstAirDate,
                VoteAverage = row.TvShow.VoteAverage,
                WatchedAt = row.LastWatchedAt,
                LastActivityAt = row.LastWatchedAt!.Value
            });

    private IQueryable<WatchedUnionRow> WatchedMovieRows(Guid userId) =>
        dbContext.WatchedMovies
            .AsNoTracking()
            .Where(watchedMovie => watchedMovie.UserId == userId)
            .Select(watchedMovie => new WatchedUnionRow
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

    private IQueryable<CompletedTvJoin> CompletedTvShows(Guid userId) =>
        TvShowCompletionQueries.StartedShows(dbContext, userId)
            .Where(TvShowCompletionQueries.IsCompleted)
            .Where(show => show.LastWatchedAt != null)
            .Join(
                dbContext.TvShows.AsNoTracking(),
                show => show.TvShowId,
                tvShow => tvShow.Id,
                (show, tvShow) => new CompletedTvJoin
                {
                    LastWatchedAt = show.LastWatchedAt,
                    TvShow = tvShow
                });

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
        LibraryNextEpisodeResult? nextEpisode) =>
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
            collectionStatus);

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

    private sealed class CompletedTvJoin
    {
        public DateTime? LastWatchedAt { get; init; }

        public Domain.Entities.TvShow TvShow { get; init; } = null!;
    }
}
