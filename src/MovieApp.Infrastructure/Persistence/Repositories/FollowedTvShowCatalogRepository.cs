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
                from follow in dbContext.CatalogFollows.AsNoTracking()
                join tvShow in dbContext.TvShows.AsNoTracking() on follow.ContentId equals tvShow.Id
                where follow.ContentType == Domain.Enums.CatalogContentType.Tv
                where tvShow.TmdbId.HasValue
                select new { tvShow.TmdbId, TvShowId = tvShow.Id })
            .Distinct()
            .ToListAsync(cancellationToken);

        return rows.ToDictionary(
            row => row.TmdbId!.Value,
            row => row.TvShowId);
    }
}
