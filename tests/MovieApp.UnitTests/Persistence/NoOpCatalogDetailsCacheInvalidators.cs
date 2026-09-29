using MovieApp.Application.Abstractions.Caching;
using MovieApp.Application.Models.Changes;

namespace MovieApp.UnitTests.Persistence;

internal sealed class NoOpMovieCatalogDetailsCacheInvalidator : IMovieCatalogDetailsCacheInvalidator
{
    public Task InvalidateAsync(Guid movieId, CancellationToken cancellationToken = default) =>
        Task.CompletedTask;
}

internal sealed class NoOpTvShowCatalogDetailsCacheInvalidator : ITvShowCatalogDetailsCacheInvalidator
{
    public Task InvalidateAsync(
        Guid tvShowId,
        IReadOnlyList<TmdbChangesHydratedSeasonCacheTarget> hydratedSeasons,
        CancellationToken cancellationToken = default) =>
        Task.CompletedTask;
}
