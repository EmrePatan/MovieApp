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

        var showsWithUnwatchedEpisodes = dbContext.Episodes
            .AsNoTracking()
            .Where(episode => episode.Season.SeasonNumber >= 1)
            .Where(episode => !dbContext.WatchedEpisodes.Any(watchedEpisode =>
                watchedEpisode.UserId == userId &&
                watchedEpisode.EpisodeId == episode.Id))
            .Select(episode => episode.Season.TvShowId)
            .Distinct();

        var watchedShows = dbContext.WatchedEpisodes
            .AsNoTracking()
            .Where(watchedEpisode => watchedEpisode.UserId == userId)
            .Where(watchedEpisode => watchedEpisode.Episode.Season.SeasonNumber >= 1)
            .GroupBy(watchedEpisode => watchedEpisode.Episode.Season.TvShowId)
            .Select(group => new
            {
                TvShowId = group.Key,
                LastWatchedAt = group.Max(watchedEpisode => watchedEpisode.WatchedAt)
            });

        var query = watchedShows
            .Where(show => showsWithUnwatchedEpisodes.Contains(show.TvShowId))
            .Join(
                dbContext.TvShows.AsNoTracking(),
                show => show.TvShowId,
                tvShow => tvShow.Id,
                (show, tvShow) => new { show.LastWatchedAt, TvShow = tvShow });

        var totalCount = await query.CountAsync(cancellationToken);

        var rows = await query
            .OrderByDescending(item => item.LastWatchedAt)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .ToListAsync(cancellationToken);

        var items = new List<LibraryItemResult>();

        foreach (var row in rows)
        {
            var regularTotalEpisodes = await dbContext.Episodes
                .AsNoTracking()
                .CountAsync(
                    episode => episode.Season.TvShowId == row.TvShow.Id &&
                               episode.Season.SeasonNumber >= 1,
                    cancellationToken);

            var regularWatchedEpisodes = await dbContext.WatchedEpisodes
                .AsNoTracking()
                .CountAsync(
                    watchedEpisode => watchedEpisode.UserId == userId &&
                                      watchedEpisode.Episode.Season.TvShowId == row.TvShow.Id &&
                                      watchedEpisode.Episode.Season.SeasonNumber >= 1,
                    cancellationToken);

            var nextEpisode = await dbContext.Episodes
                .AsNoTracking()
                .Include(episode => episode.Season)
                .Where(episode =>
                    episode.Season.TvShowId == row.TvShow.Id &&
                    episode.Season.SeasonNumber >= 1)
                .Where(episode => !dbContext.WatchedEpisodes.Any(
                    watchedEpisode =>
                        watchedEpisode.UserId == userId &&
                        watchedEpisode.EpisodeId == episode.Id))
                .OrderBy(episode => episode.Season.SeasonNumber)
                .ThenBy(episode => episode.EpisodeNumber)
                .FirstOrDefaultAsync(cancellationToken);

            items.Add(MapTvShow(
                row.TvShow,
                CollectionStatusWatching,
                addedAt: null,
                watchedAt: null,
                lastActivityAt: row.LastWatchedAt,
                progressPercentage: WatchHistoryMapper.CalculateProgressPercentage(
                    regularWatchedEpisodes,
                    regularTotalEpisodes),
                nextEpisode: nextEpisode is null
                    ? null
                    : new LibraryNextEpisodeResult(
                        nextEpisode.Id,
                        nextEpisode.Season.SeasonNumber,
                        nextEpisode.EpisodeNumber,
                        nextEpisode.Name)));
        }

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
            .OrderByDescending(item => item.AddedAt);

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
        var rows = await dbContext.TvShows
            .AsNoTracking()
            .Where(tvShow => dbContext.WatchedEpisodes.Any(
                watchedEpisode => watchedEpisode.UserId == userId &&
                                  watchedEpisode.Episode.Season.TvShowId == tvShow.Id))
            .Select(tvShow => new
            {
                TvShow = tvShow,
                TotalEpisodes = tvShow.Seasons
                    .Where(season => season.SeasonNumber >= 1)
                    .SelectMany(season => season.Episodes)
                    .Count(),
                WatchedEpisodes = dbContext.WatchedEpisodes.Count(
                    watchedEpisode => watchedEpisode.UserId == userId &&
                                      watchedEpisode.Episode.Season.TvShowId == tvShow.Id &&
                                      watchedEpisode.Episode.Season.SeasonNumber >= 1),
                LastWatchedAt = dbContext.WatchedEpisodes
                    .Where(watchedEpisode => watchedEpisode.UserId == userId &&
                                             watchedEpisode.Episode.Season.TvShowId == tvShow.Id &&
                                             watchedEpisode.Episode.Season.SeasonNumber >= 1)
                    .Max(watchedEpisode => (DateTime?)watchedEpisode.WatchedAt)
            })
            .Where(show => show.TotalEpisodes > 0 &&
                           show.WatchedEpisodes >= show.TotalEpisodes &&
                           show.LastWatchedAt != null)
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
