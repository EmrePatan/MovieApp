using Microsoft.EntityFrameworkCore;
using MovieApp.Application.Abstractions.Persistence;
using MovieApp.Domain.Entities;

namespace MovieApp.Infrastructure.Persistence.Repositories;

public sealed class ReleaseDetectionCatalogRepository(ApplicationDbContext dbContext)
    : IReleaseDetectionCatalogRepository
{
    public async Task<IReadOnlyList<Season>> GetSeasonsWithEpisodesAsync(
        Guid tvShowId,
        CancellationToken cancellationToken = default)
    {
        return await dbContext.Seasons
            .AsNoTracking()
            .Include(season => season.Episodes)
            .Where(season => season.TvShowId == tvShowId)
            .ToListAsync(cancellationToken);
    }

    public async Task<IReadOnlyList<int>> GetSeasonNumbersNeedingEpisodeHydrationAsync(
        Guid tvShowId,
        DateOnly boundaryDate,
        CancellationToken cancellationToken = default)
    {
        return await dbContext.Seasons
            .AsNoTracking()
            .Where(season =>
                season.TvShowId == tvShowId &&
                season.SeasonNumber >= 1 &&
                season.EpisodeCount.HasValue &&
                season.EpisodeCount.Value > 0 &&
                ((season.AirDate.HasValue && season.AirDate.Value <= boundaryDate) ||
                 season.Episodes.Any(episode =>
                     episode.EpisodeNumber >= 1 &&
                     episode.AirDate.HasValue &&
                     episode.AirDate.Value <= boundaryDate)) &&
                season.Episodes.Count(episode => episode.EpisodeNumber >= 1) < season.EpisodeCount.Value)
            .OrderBy(season => season.SeasonNumber)
            .Select(season => season.SeasonNumber)
            .ToListAsync(cancellationToken);
    }
}
