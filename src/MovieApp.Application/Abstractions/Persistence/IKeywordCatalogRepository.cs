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

    Task<KeywordEnrichmentTarget?> GetMovieMdbListKeywordTargetAsync(
        Guid movieId,
        CancellationToken cancellationToken = default);

    Task<KeywordEnrichmentTarget?> GetTvShowMdbListKeywordTargetAsync(
        Guid tvShowId,
        CancellationToken cancellationToken = default);

    Task<Guid?> FindMovieIdByTmdbIdAsync(int tmdbId, CancellationToken cancellationToken = default);

    Task<Guid?> FindTvShowIdByTmdbIdAsync(int tmdbId, CancellationToken cancellationToken = default);

    Task<MdbListKeywordIngestionResult> ApplyMovieMdbListKeywordIngestionAsync(
        Guid movieId,
        IReadOnlyList<MdbListKeywordTransportItem> providerKeywords,
        DateTime syncedAtUtc,
        CancellationToken cancellationToken = default);

    Task<MdbListKeywordIngestionResult> ApplyTvShowMdbListKeywordIngestionAsync(
        Guid tvShowId,
        IReadOnlyList<MdbListKeywordTransportItem> providerKeywords,
        DateTime syncedAtUtc,
        CancellationToken cancellationToken = default);

    Task<int> CountMovieMdbListSourcesAsync(Guid movieId, CancellationToken cancellationToken = default);

    Task<int> CountTvShowMdbListSourcesAsync(Guid tvShowId, CancellationToken cancellationToken = default);
}
