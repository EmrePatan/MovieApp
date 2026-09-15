using MovieApp.Application.Models.Providers;
using MovieApp.Domain.Entities;

namespace MovieApp.Application.Abstractions.Persistence;

public interface IMovieRepository
{
    Task<Movie?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default);

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
