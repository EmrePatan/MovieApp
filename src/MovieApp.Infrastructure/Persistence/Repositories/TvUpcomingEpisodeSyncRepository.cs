using Microsoft.EntityFrameworkCore;
using MovieApp.Application.Abstractions.Persistence;
using MovieApp.Application.Models.TvUpcomingEpisodes;
using MovieApp.Domain.Enums;

namespace MovieApp.Infrastructure.Persistence.Repositories;

public sealed class TvUpcomingEpisodeSyncRepository(ApplicationDbContext dbContext)
    : ITvUpcomingEpisodeSyncRepository
{
    public async Task<IReadOnlyList<TvUpcomingEpisodeSyncCandidate>> SelectStaleFollowedShowsAsync(
        int batchSize,
        DateTime staleBeforeUtc,
        CancellationToken cancellationToken = default)
    {
        if (batchSize <= 0)
        {
            return [];
        }

        return await (
                from follow in dbContext.CatalogFollows.AsNoTracking()
                where follow.ContentType == CatalogContentType.Tv
                join tvShow in dbContext.TvShows.AsNoTracking() on follow.ContentId equals tvShow.Id
                join sync in dbContext.TvShowCatalogSyncStates.AsNoTracking()
                    on tvShow.Id equals sync.TvShowId into syncJoin
                from sync in syncJoin.DefaultIfEmpty()
                where sync == null ||
                      sync.LastUpcomingEpisodeSyncAtUtc == null ||
                      sync.LastUpcomingEpisodeSyncAtUtc < staleBeforeUtc
                select new TvUpcomingEpisodeSyncCandidate(
                    tvShow.Id,
                    tvShow.TmdbId,
                    tvShow.TvdbId,
                    tvShow.ImdbId))
            .Distinct()
            .OrderBy(candidate => candidate.TvShowId)
            .Take(batchSize)
            .ToListAsync(cancellationToken);
    }
}
