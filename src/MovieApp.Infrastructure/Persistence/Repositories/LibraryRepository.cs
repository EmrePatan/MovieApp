using Microsoft.EntityFrameworkCore;
using MovieApp.Application.Abstractions.Persistence;
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
        int page,
        int pageSize,
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

        var totalCount = await query.CountAsync(cancellationToken);

        var rows = await query
            .OrderByDescending(item => item.RegularWatchedEpisodes < item.RegularTotalEpisodes)
            .ThenByDescending(item => item.LastWatchedAt)
            .ThenBy(item => item.TvShow.Id)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
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
        int page,
        int pageSize,
        CancellationToken cancellationToken = default)
    {
        if (mediaType == SearchContentType.Movie)
        {
            return await GetWatchedMoviesAsync(userId, page, pageSize, cancellationToken);
        }

        if (mediaType == SearchContentType.Tv)
        {
            return await GetWatchedTvShowsAsync(userId, page, pageSize, cancellationToken);
        }

        var movieRows = await dbContext.WatchedMovies
            .AsNoTracking()
            .Where(watchedMovie => watchedMovie.UserId == userId)
            .Select(watchedMovie => new WatchedUnionRow(
                watchedMovie.MovieId,
                "movie",
                watchedMovie.Movie.Title,
                watchedMovie.Movie.OriginalTitle,
                watchedMovie.Movie.PosterPath,
                watchedMovie.Movie.BackdropPath,
                watchedMovie.Movie.ReleaseDate,
                null,
                watchedMovie.Movie.VoteAverage,
                watchedMovie.WatchedAt,
                watchedMovie.WatchedAt))
            .ToListAsync(cancellationToken);

        var tvRows = await GetCompletedTvUnionRowsAsync(userId, cancellationToken);

        var merged = movieRows
            .Concat(tvRows)
            .OrderByDescending(row => row.LastActivityAt)
            .ThenBy(row => row.Type, StringComparer.Ordinal)
            .ThenBy(row => row.Id)
            .ToList();

        var totalCount = merged.Count;
        var pageRows = merged
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .ToList();

        return (pageRows.Select(MapUnionRow).ToList(), totalCount);
    }

    public async Task<(IReadOnlyList<LibraryItemResult> Items, int TotalCount)> GetLikedAsync(
        Guid userId,
        SearchContentType mediaType,
        int page,
        int pageSize,
        CancellationToken cancellationToken = default)
    {
        var query = dbContext.Favorites
            .AsNoTracking()
            .Where(favorite => favorite.UserId == userId);

        query = ApplyFavoriteMediaTypeFilter(query, mediaType);

        var totalCount = await query.CountAsync(cancellationToken);

        var favorites = await query
            .Include(favorite => favorite.Movie)
            .Include(favorite => favorite.TvShow)
            .OrderByDescending(favorite => favorite.CreatedAt)
            .ThenBy(favorite => favorite.Id)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
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
        int page,
        int pageSize,
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

        var totalCount = await dedupedQuery.CountAsync(cancellationToken);

        var pageKeys = await dedupedQuery
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
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
        int page,
        int pageSize,
        CancellationToken cancellationToken)
    {
        var query = dbContext.WatchedMovies
            .AsNoTracking()
            .Where(watchedMovie => watchedMovie.UserId == userId);

        var totalCount = await query.CountAsync(cancellationToken);

        var watchedMovies = await query
            .Include(watchedMovie => watchedMovie.Movie)
            .OrderByDescending(watchedMovie => watchedMovie.WatchedAt)
            .ThenBy(watchedMovie => watchedMovie.MovieId)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
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
        int page,
        int pageSize,
        CancellationToken cancellationToken)
    {
        var rows = await GetCompletedTvUnionRowsAsync(userId, cancellationToken);
        var totalCount = rows.Count;

        var pageRows = rows
            .OrderByDescending(row => row.LastActivityAt)
            .ThenBy(row => row.Id)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .Select(MapUnionRow)
            .ToList();

        return (pageRows, totalCount);
    }

    private async Task<List<WatchedUnionRow>> GetCompletedTvUnionRowsAsync(
        Guid userId,
        CancellationToken cancellationToken)
    {
        var rows = await TvShowCompletionQueries.StartedShows(dbContext, userId)
            .Where(TvShowCompletionQueries.IsCompleted)
            .Where(show => show.LastWatchedAt != null)
            .Join(
                dbContext.TvShows.AsNoTracking(),
                show => show.TvShowId,
                tvShow => tvShow.Id,
                (show, tvShow) => new { show.LastWatchedAt, TvShow = tvShow })
            .ToListAsync(cancellationToken);

        return rows
            .Select(row => new WatchedUnionRow(
                row.TvShow.Id,
                "tv",
                row.TvShow.Title,
                row.TvShow.OriginalTitle,
                row.TvShow.PosterPath,
                row.TvShow.BackdropPath,
                null,
                row.TvShow.FirstAirDate,
                row.TvShow.VoteAverage,
                row.LastWatchedAt,
                row.LastWatchedAt!.Value))
            .ToList();
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

    private sealed record WatchedUnionRow(
        Guid Id,
        string Type,
        string Title,
        string? OriginalTitle,
        string? PosterUrl,
        string? BackdropUrl,
        DateOnly? ReleaseDate,
        DateOnly? FirstAirDate,
        decimal VoteAverage,
        DateTime? WatchedAt,
        DateTime LastActivityAt);
}
