using MovieApp.Application.Models.Keywords;
using MovieApp.Application.Models.Providers;

namespace MovieApp.Application.Abstractions.Persistence;

public interface IKeywordCatalogRepository
{
    Task<KeywordEnrichmentTarget?> GetMovieKeywordTargetAsync(
        Guid movieId,
        CancellationToken cancellationToken = default);

    Task<KeywordEnrichmentTarget?> GetTvShowKeywordTargetAsync(
        Guid tvShowId,
        CancellationToken cancellationToken = default);

    Task SyncMovieKeywordsAsync(
        Guid movieId,
        IReadOnlyList<ProviderKeywordSummary> keywords,
        DateTime syncedAtUtc,
        CancellationToken cancellationToken = default);

    Task SyncTvShowKeywordsAsync(
        Guid tvShowId,
        IReadOnlyList<ProviderKeywordSummary> keywords,
        DateTime syncedAtUtc,
        CancellationToken cancellationToken = default);
}
