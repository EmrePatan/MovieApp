using MovieApp.Application.Models.Catalog;
using MovieApp.Application.Models.Providers;
using MovieApp.Domain.Entities;
using MovieApp.Domain.Enums;

namespace MovieApp.Application.Abstractions.Persistence;

public interface ITvShowRepository
{
    Task<TvShow?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default);

    async Task<bool> ExistsAsync(Guid id, CancellationToken cancellationToken = default)
    {
        var tvShow = await GetByIdAsync(id, cancellationToken);
        return tvShow is not null;
    }

    async Task<TvShowStatus?> GetStatusAsync(Guid id, CancellationToken cancellationToken = default)
    {
        var tvShow = await GetByIdAsync(id, cancellationToken);
        return tvShow?.Status;
    }

    async Task<CatalogProviderLookup?> GetProviderLookupByIdAsync(
        Guid id,
        CancellationToken cancellationToken = default)
    {
        var tvShow = await GetByIdAsync(id, cancellationToken);
        return tvShow is null
            ? null
            : new CatalogProviderLookup(tvShow.TmdbId, tvShow.OriginalLanguage);
    }

    async Task<TvShowExternalIds?> GetExternalIdsByIdAsync(
        Guid id,
        CancellationToken cancellationToken = default)
    {
        var tvShow = await GetByIdAsync(id, cancellationToken);
        return tvShow is null
            ? null
            : new TvShowExternalIds(tvShow.TmdbId, tvShow.TvdbId, tvShow.ImdbId);
    }

    Task<IReadOnlyDictionary<Guid, TvShow>> GetByIdsAsync(
        IReadOnlyList<Guid> ids,
        CancellationToken cancellationToken = default) =>
        throw new NotSupportedException();

    Task<TvShow?> GetByTmdbIdAsync(int tmdbId, CancellationToken cancellationToken = default);

    Task<IReadOnlyDictionary<Guid, int>> GetTmdbIdsByIdsAsync(
        IReadOnlyList<Guid> ids,
        CancellationToken cancellationToken = default) =>
        throw new NotSupportedException();

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
