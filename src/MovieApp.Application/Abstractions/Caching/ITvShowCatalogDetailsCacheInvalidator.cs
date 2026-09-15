using MovieApp.Application.Models.Changes;

namespace MovieApp.Application.Abstractions.Caching;

public interface ITvShowCatalogDetailsCacheInvalidator
{
    Task InvalidateAsync(
        Guid tvShowId,
        IReadOnlyList<TmdbChangesHydratedSeasonCacheTarget> hydratedSeasons,
        CancellationToken cancellationToken = default);
}
