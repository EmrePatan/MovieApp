using Microsoft.EntityFrameworkCore;
using MovieApp.Application.Abstractions.Persistence;
using MovieApp.Application.Models.HotRelease;
using MovieApp.Application.Services.HotRelease;
using MovieApp.Domain.Notifications;

namespace MovieApp.Infrastructure.Persistence.Repositories;

public sealed class HotReleaseCandidateRepository(ApplicationDbContext dbContext)
    : IHotReleaseCandidateRepository
{
    public async Task<IReadOnlyList<HotReleaseCandidate>> GetCandidatesAsync(
        DateOnly boundaryDate,
        CancellationToken cancellationToken = default)
    {
        var (windowStart, windowEnd) = HotReleaseCheckWindow.ForBoundary(boundaryDate);
        var boundaryAtUtc = ReleaseDateTime.ToReleaseAtUtc(boundaryDate);

        var followedTvShowIds = dbContext.TvShowFollows
            .AsNoTracking()
            .Select(follow => follow.TvShowId)
            .Distinct();

        var candidates = await dbContext.TvShows
            .AsNoTracking()
            .Where(tvShow => followedTvShowIds.Contains(tvShow.Id))
            .Where(tvShow =>
                dbContext.Seasons.Any(season =>
                    season.TvShowId == tvShow.Id &&
                    season.SeasonNumber >= 1 &&
                    (
                        (season.AirDate.HasValue &&
                         season.AirDate.Value >= windowStart &&
                         season.AirDate.Value <= windowEnd) ||
                        season.Episodes.Any(episode =>
                            episode.EpisodeNumber >= 1 &&
                            episode.AirDate.HasValue &&
                            episode.AirDate.Value >= windowStart &&
                            episode.AirDate.Value <= windowEnd))) ||
                dbContext.TvShowCatalogSyncStates.Any(syncState =>
                    syncState.TvShowId == tvShow.Id &&
                    syncState.NextHotCheckAtUtc.HasValue &&
                    syncState.NextHotCheckAtUtc.Value <= boundaryAtUtc))
            .Select(tvShow => new HotReleaseCandidate(
                tvShow.Id,
                tvShow.TmdbId,
                tvShow.TvdbId,
                tvShow.ImdbId))
            .ToListAsync(cancellationToken);

        return candidates;
    }
}
