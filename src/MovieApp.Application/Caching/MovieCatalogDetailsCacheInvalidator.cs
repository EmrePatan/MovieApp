using MovieApp.Application.Abstractions.Caching;

namespace MovieApp.Application.Caching;

public sealed class MovieCatalogDetailsCacheInvalidator(ICacheService cacheService)
    : IMovieCatalogDetailsCacheInvalidator
{
    public Task InvalidateAsync(Guid movieId, CancellationToken cancellationToken = default) =>
        cacheService.RemoveAsync(MovieDetailsCacheKeys.Create(movieId), cancellationToken);
}
