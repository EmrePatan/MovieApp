using Microsoft.EntityFrameworkCore;
using MovieApp.Application.Models.WatchHistory;
using MovieApp.Domain.Enums;

namespace MovieApp.Infrastructure.Persistence.Repositories;

/// <summary>One database-paged timeline over watched movies and episodes.</summary>
internal static class WatchHistoryPageQuery
{
    private sealed class Row
    {
        public bool IsMovie { get; init; }

        public Guid ContentId { get; init; }

        public Guid GroupId { get; init; }

        public int SeasonNumber { get; init; }

        public int EpisodeNumber { get; init; }

        public DateTime WatchedAt { get; init; }

        public WatchDatePrecision Precision { get; init; }
    }

    public static async Task<(IReadOnlyList<RecentWatchHistoryItemResult> Items, int TotalCount)> LoadAsync(
        ApplicationDbContext dbContext,
        Guid userId,
        bool? undated,
        int skip,
        int take,
        CancellationToken cancellationToken)
    {
        var watchedMovies = dbContext.WatchedMovies.AsNoTracking().Where(watched => watched.UserId == userId);
        var watchedEpisodes = dbContext.WatchedEpisodes.AsNoTracking().Where(watched => watched.UserId == userId);

        if (undated is { } onlyUndated)
        {
            watchedMovies = watchedMovies.Where(watched =>
                (watched.WatchedAtPrecision == WatchDatePrecision.Unknown) == onlyUndated);
            watchedEpisodes = watchedEpisodes.Where(watched =>
                (watched.WatchedAtPrecision == WatchDatePrecision.Unknown) == onlyUndated);
        }

        var totalCount = await watchedMovies.CountAsync(cancellationToken)
            + await watchedEpisodes.CountAsync(cancellationToken);

        var rows = await watchedMovies
            .Select(watched => new Row
            {
                IsMovie = true,
                ContentId = watched.MovieId,
                GroupId = watched.MovieId,
                SeasonNumber = 0,
                EpisodeNumber = 0,
                WatchedAt = watched.WatchedAt,
                Precision = watched.WatchedAtPrecision,
            })
            .Concat(watchedEpisodes.Select(watched => new Row
            {
                IsMovie = false,
                ContentId = watched.EpisodeId,
                GroupId = watched.Episode.Season.TvShowId,
                SeasonNumber = watched.Episode.Season.SeasonNumber,
                EpisodeNumber = watched.Episode.EpisodeNumber,
                WatchedAt = watched.WatchedAt,
                Precision = watched.WatchedAtPrecision,
            }))
            // Undated rows sort last. The remaining keys make the order total: a bulk mark gives
            // hundreds of rows one timestamp, and pages must not overlap or skip rows then.
            .OrderBy(row => row.Precision == WatchDatePrecision.Unknown)
            .ThenByDescending(row => row.WatchedAt)
            .ThenBy(row => row.GroupId)
            .ThenByDescending(row => row.SeasonNumber)
            .ThenByDescending(row => row.EpisodeNumber)
            .ThenBy(row => row.ContentId)
            .Skip(skip)
            .Take(take)
            .ToListAsync(cancellationToken);

        var movieIds = rows.Where(row => row.IsMovie).Select(row => row.ContentId).ToList();
        var movies = await dbContext.Movies
            .AsNoTracking()
            .Where(movie => movieIds.Contains(movie.Id))
            .Select(movie => new { movie.Id, movie.Title, movie.PosterPath })
            .ToDictionaryAsync(movie => movie.Id, cancellationToken);

        var episodeIds = rows.Where(row => !row.IsMovie).Select(row => row.ContentId).ToList();
        var episodes = await dbContext.Episodes
            .AsNoTracking()
            .Where(episode => episodeIds.Contains(episode.Id))
            .Select(episode => new
            {
                episode.Id,
                episode.Name,
                ShowTitle = episode.Season.TvShow.Title,
                ShowPosterPath = episode.Season.TvShow.PosterPath,
            })
            .ToDictionaryAsync(episode => episode.Id, cancellationToken);

        var items = rows
            .Select(row =>
            {
                var precision = row.Precision.ToString().ToLowerInvariant();
                if (row.IsMovie)
                {
                    var movie = movies[row.ContentId];
                    return new RecentWatchHistoryItemResult(
                        "movie", row.ContentId, null, null, movie.Title,
                        null, null, null, null, row.WatchedAt, precision, movie.PosterPath);
                }

                var episode = episodes[row.ContentId];
                return new RecentWatchHistoryItemResult(
                    "episode", null, row.ContentId, row.GroupId, null,
                    episode.ShowTitle, row.SeasonNumber, row.EpisodeNumber, episode.Name,
                    row.WatchedAt, precision, episode.ShowPosterPath);
            })
            .ToList();

        return (items, totalCount);
    }
}
