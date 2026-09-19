using MovieApp.Application.Models.Catalog;
using MovieApp.Application.Models.Providers;
using MovieApp.Domain.Entities;

namespace MovieApp.Application.Abstractions.Persistence;

public interface IMovieRepository
{
    Task<Movie?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default);

    async Task<CatalogProviderLookup?> GetProviderLookupByIdAsync(
        Guid id,
        CancellationToken cancellationToken = default)
    {
        var movie = await GetByIdAsync(id, cancellationToken);
        return movie is null
            ? null
            : new CatalogProviderLookup(movie.TmdbId, movie.OriginalLanguage);
    }

    Task<Movie?> GetByTmdbIdAsync(int tmdbId, CancellationToken cancellationToken = default);

    Task<IReadOnlyDictionary<int, Guid>> GetExistingIdsByTmdbIdsAsync(
        IReadOnlyList<int> tmdbIds,
        CancellationToken cancellationToken = default) =>
        throw new NotSupportedException();

    Task<Movie> UpsertFromProviderAsync(
        MovieProviderDetails details,
        CancellationToken cancellationToken = default);

    Task<IReadOnlyList<Movie>> UpsertFromProviderBatchAsync(
        IReadOnlyList<MovieProviderDetails> details,
        CancellationToken cancellationToken = default) =>
        throw new NotSupportedException();

    Task<IReadOnlyDictionary<int, Guid>> EnsureFromSummariesAsync(
        IReadOnlyList<MovieProviderSummary> summaries,
        CancellationToken cancellationToken = default) =>
        throw new NotSupportedException();
}
