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
}
