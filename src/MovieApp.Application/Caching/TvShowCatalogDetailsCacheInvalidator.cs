using MovieApp.Application.Abstractions.Caching;
using MovieApp.Application.Models.Changes;

namespace MovieApp.Application.Caching;

public sealed class TvShowCatalogDetailsCacheInvalidator(ICacheService cacheService)
    : ITvShowCatalogDetailsCacheInvalidator
{
    public async Task InvalidateAsync(
        Guid tvShowId,
        IReadOnlyList<TmdbChangesHydratedSeasonCacheTarget> hydratedSeasons,
        CancellationToken cancellationToken = default)
    {
        await cacheService.RemoveAsync(TvShowDetailsCacheKeys.Create(tvShowId), cancellationToken);

        foreach (var hydratedSeason in hydratedSeasons)
        {
            await cacheService.RemoveAsync(
                TvShowSeasonCacheKeys.Create(tvShowId, hydratedSeason.SeasonNumber),
                cancellationToken);

            foreach (var episodeNumber in hydratedSeason.EpisodeNumbers)
            {
                await cacheService.RemoveAsync(
                    TvShowEpisodeCacheKeys.Create(tvShowId, hydratedSeason.SeasonNumber, episodeNumber),
                    cancellationToken);
            }
        }
    }
}
