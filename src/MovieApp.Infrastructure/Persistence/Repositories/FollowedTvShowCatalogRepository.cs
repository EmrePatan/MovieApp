using Microsoft.EntityFrameworkCore;
using MovieApp.Application.Abstractions.Persistence;

namespace MovieApp.Infrastructure.Persistence.Repositories;

public sealed class FollowedTvShowCatalogRepository(ApplicationDbContext dbContext)
    : IFollowedTvShowCatalogRepository
{
    public async Task<IReadOnlyDictionary<int, Guid>> GetFollowedTvShowIdsByTmdbIdAsync(
        CancellationToken cancellationToken = default)
    {
        var rows = await (
                from follow in dbContext.TvShowFollows.AsNoTracking()
                join tvShow in dbContext.TvShows.AsNoTracking() on follow.TvShowId equals tvShow.Id
                where tvShow.TmdbId.HasValue
                select new { tvShow.TmdbId, follow.TvShowId })
            .Distinct()
            .ToListAsync(cancellationToken);

        return rows.ToDictionary(
            row => row.TmdbId!.Value,
            row => row.TvShowId);
    }
}
