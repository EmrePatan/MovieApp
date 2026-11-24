using Microsoft.EntityFrameworkCore;
using MovieApp.Application.Abstractions.Persistence;
using MovieApp.Application.Models.WatchHistory;
using MovieApp.Application.Services.WatchHistory;

namespace MovieApp.Infrastructure.Persistence.Repositories;

public sealed class WatchStatusBatchReadRepository(ApplicationDbContext dbContext)
    : IWatchStatusBatchReadRepository
{
    public async Task<IReadOnlyList<WatchedMovieStatusRow>> GetWatchedMoviesAsync(
        Guid userId,
        IReadOnlyCollection<Guid> movieIds,
        CancellationToken cancellationToken = default)
    {
        if (movieIds.Count == 0)
        {
            return [];
        }

        var ids = movieIds.ToArray();
        var rows = await dbContext.WatchedMovies
            .AsNoTracking()
            .Where(watched => watched.UserId == userId && ids.Contains(watched.MovieId))
            .Select(watched => new { watched.MovieId, watched.WatchedAt })
            .ToListAsync(cancellationToken);

        return rows
            .Select(row => new WatchedMovieStatusRow(row.MovieId, row.WatchedAt))
            .ToList();
    }

    public async Task<IReadOnlyList<TvShowRegularProgressRow>> GetTvShowRegularProgressAsync(
        Guid userId,
        IReadOnlyCollection<Guid> tvShowIds,
        CancellationToken cancellationToken = default)
    {
        if (tvShowIds.Count == 0)
        {
            return [];
        }

        var ids = tvShowIds.ToArray();
        var today = EpisodeWatchEligibility.TodayUtc();

        // Mirrors EpisodeRepository.GetEpisodeCountsBySeasonAsync for regular seasons only.
        var seasonRows = await dbContext.Seasons
            .AsNoTracking()
            .Where(season => ids.Contains(season.TvShowId) && season.SeasonNumber >= 1)
            .Select(season => new
            {
                season.TvShowId,
                season.SeasonNumber,
                season.AirDate,
                IngestedEpisodeCount = season.Episodes.Count(),
                ReleasedEpisodeCount = season.Episodes.Count(episode =>
                    episode.AirDate != null && episode.AirDate <= today),
                SummaryEpisodeCount = season.EpisodeCount,
            })
            .ToListAsync(cancellationToken);

        var totalsBySeason = seasonRows
            .Select(row => new
            {
                row.TvShowId,
                row.SeasonNumber,
                EpisodeCount = row.IngestedEpisodeCount > 0
                    ? row.SummaryEpisodeCount is > 0 &&
                      row.IngestedEpisodeCount < row.SummaryEpisodeCount.Value
                        ? row.SummaryEpisodeCount.Value
                        : row.ReleasedEpisodeCount
                    : row.AirDate is null || row.AirDate <= today
                        ? Math.Max(0, row.SummaryEpisodeCount ?? 0)
                        : 0,
            })
            .Where(row => row.EpisodeCount > 0)
            .ToList();

        // Mirrors WatchedEpisodeRepository.GetWatchedEpisodeCountsBySeasonAsync.
        var watchedRows = await dbContext.WatchedEpisodes
            .AsNoTracking()
            .Where(watched =>
                watched.UserId == userId &&
                ids.Contains(watched.Episode.Season.TvShowId) &&
                watched.Episode.Season.SeasonNumber >= 1 &&
                watched.Episode.AirDate != null &&
                watched.Episode.AirDate <= today)
            .GroupBy(watched => new
            {
                watched.Episode.Season.TvShowId,
                watched.Episode.Season.SeasonNumber,
            })
            .Select(group => new
            {
                group.Key.TvShowId,
                group.Key.SeasonNumber,
                EpisodeCount = group.Count(),
            })
            .ToListAsync(cancellationToken);

        var watchedBySeason = watchedRows.ToDictionary(
            row => (row.TvShowId, row.SeasonNumber),
            row => row.EpisodeCount);

        // Watched episodes only count for seasons that have a non-zero total, as in the
        // single-show progress endpoint.
        return totalsBySeason
            .GroupBy(row => row.TvShowId)
            .Select(group => new TvShowRegularProgressRow(
                group.Key,
                group.Sum(row => row.EpisodeCount),
                group.Sum(row => watchedBySeason.GetValueOrDefault((row.TvShowId, row.SeasonNumber)))))
            .ToList();
    }
}
