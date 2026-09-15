using MovieApp.Application.Models.Providers;
using MovieApp.Domain.Entities;

namespace MovieApp.Application.Abstractions.Persistence;

public interface ITvShowRepository
{
    Task<TvShow?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default);

    Task<IReadOnlyDictionary<Guid, TvShow>> GetByIdsAsync(
        IReadOnlyList<Guid> ids,
        CancellationToken cancellationToken = default) =>
        throw new NotSupportedException();

    Task<TvShow?> GetByTmdbIdAsync(int tmdbId, CancellationToken cancellationToken = default);

    Task<IReadOnlyDictionary<int, Guid>> GetExistingIdsByTmdbIdsAsync(
        IReadOnlyList<int> tmdbIds,
        CancellationToken cancellationToken = default) =>
        throw new NotSupportedException();

    Task<TvShow> UpsertFromProviderAsync(
        TvShowProviderDetails details,
        CancellationToken cancellationToken = default);

    Task<IReadOnlyList<TvShow>> UpsertFromProviderBatchAsync(
        IReadOnlyList<TvShowProviderDetails> details,
        CancellationToken cancellationToken = default) =>
        throw new NotSupportedException();

    Task<IReadOnlyDictionary<int, Guid>> EnsureFromSummariesAsync(
        IReadOnlyList<TvShowProviderSummary> summaries,
        CancellationToken cancellationToken = default) =>
        throw new NotSupportedException();
}
