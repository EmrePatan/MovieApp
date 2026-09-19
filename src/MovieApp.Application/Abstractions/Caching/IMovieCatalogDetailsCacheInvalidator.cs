namespace MovieApp.Application.Abstractions.Caching;

public interface IMovieCatalogDetailsCacheInvalidator
{
    Task InvalidateAsync(Guid movieId, CancellationToken cancellationToken = default);
}
