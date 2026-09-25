using System.Linq.Expressions;
using Microsoft.EntityFrameworkCore;
using MovieApp.Domain.Enums;

namespace MovieApp.Infrastructure.Persistence.Repositories;

/// <summary>
/// SQL-translatable form of <see cref="MovieApp.Application.Services.WatchHistory.TvShowCompletionPolicy"/>.
/// </summary>
internal static class TvShowCompletionQueries
{
    internal static readonly Expression<Func<TvShowCompletionRow, bool>> IsCompleted = row =>
        row.IsConcluded &&
        row.RegularTotalEpisodes > 0 &&
        row.RegularWatchedEpisodes >= row.RegularTotalEpisodes;

    internal static readonly Expression<Func<TvShowCompletionRow, bool>> IsInProgress = row =>
        !(row.IsConcluded &&
          row.RegularTotalEpisodes > 0 &&
          row.RegularWatchedEpisodes >= row.RegularTotalEpisodes);

    /// <summary>
    /// One row per show in which the user has watched at least one regular (season ≥ 1) episode.
    /// </summary>
    internal static IQueryable<TvShowCompletionRow> StartedShows(ApplicationDbContext dbContext, Guid userId)
    {
        var startedShowIds = dbContext.WatchedEpisodes
            .AsNoTracking()
            .Where(watchedEpisode =>
                watchedEpisode.UserId == userId &&
                watchedEpisode.Episode.Season.SeasonNumber >= 1)
            .Select(watchedEpisode => watchedEpisode.Episode.Season.TvShowId)
            .Distinct();

        return dbContext.TvShows
            .AsNoTracking()
            .Where(tvShow => startedShowIds.Contains(tvShow.Id))
            .Select(tvShow => new TvShowCompletionRow
            {
                TvShowId = tvShow.Id,
                IsConcluded = tvShow.Status == TvShowStatus.Ended || tvShow.Status == TvShowStatus.Canceled,
                RegularTotalEpisodes =
                    dbContext.Episodes.Count(episode =>
                        episode.Season.TvShowId == tvShow.Id &&
                        episode.Season.SeasonNumber >= 1) +
                    tvShow.Seasons
                        .Where(season => season.SeasonNumber >= 1 && season.Episodes.Count == 0)
                        .Sum(season => season.EpisodeCount ?? 0),
                RegularWatchedEpisodes = dbContext.WatchedEpisodes.Count(watchedEpisode =>
                    watchedEpisode.UserId == userId &&
                    watchedEpisode.Episode.Season.TvShowId == tvShow.Id &&
                    watchedEpisode.Episode.Season.SeasonNumber >= 1),
                LastWatchedAt = dbContext.WatchedEpisodes
                    .Where(watchedEpisode =>
                        watchedEpisode.UserId == userId &&
                        watchedEpisode.Episode.Season.TvShowId == tvShow.Id &&
                        watchedEpisode.Episode.Season.SeasonNumber >= 1)
                    .Max(watchedEpisode => (DateTime?)watchedEpisode.WatchedAt),
            });
    }
}

internal sealed class TvShowCompletionRow
{
    public Guid TvShowId { get; init; }

    public bool IsConcluded { get; init; }

    public int RegularTotalEpisodes { get; init; }

    public int RegularWatchedEpisodes { get; init; }

    public DateTime? LastWatchedAt { get; init; }
}
