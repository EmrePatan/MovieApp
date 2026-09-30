using MovieApp.Application.Abstractions.Persistence;
using MovieApp.Application.Models.Keywords;

namespace MovieApp.UnitTests.Keywords;

internal static class KeywordCatalogRepositoryMdbListNoOp
{
    public static Task<KeywordEnrichmentTarget?> GetMovieMdbListKeywordTargetAsync(
        Guid movieId,
        CancellationToken cancellationToken = default) =>
        Task.FromResult<KeywordEnrichmentTarget?>(null);

    public static Task<KeywordEnrichmentTarget?> GetTvShowMdbListKeywordTargetAsync(
        Guid tvShowId,
        CancellationToken cancellationToken = default) =>
        Task.FromResult<KeywordEnrichmentTarget?>(null);

    public static Task<Guid?> FindMovieIdByTmdbIdAsync(int tmdbId, CancellationToken cancellationToken = default) =>
        Task.FromResult<Guid?>(null);

    public static Task<Guid?> FindTvShowIdByTmdbIdAsync(int tmdbId, CancellationToken cancellationToken = default) =>
        Task.FromResult<Guid?>(null);

    public static Task<MdbListKeywordIngestionResult> ApplyMovieMdbListKeywordIngestionAsync(
        Guid movieId,
        IReadOnlyList<MdbListKeywordTransportItem> providerKeywords,
        DateTime syncedAtUtc,
        CancellationToken cancellationToken = default) =>
        throw new NotSupportedException();

    public static Task<MdbListKeywordIngestionResult> ApplyTvShowMdbListKeywordIngestionAsync(
        Guid tvShowId,
        IReadOnlyList<MdbListKeywordTransportItem> providerKeywords,
        DateTime syncedAtUtc,
        CancellationToken cancellationToken = default) =>
        throw new NotSupportedException();

    public static Task<int> CountMovieMdbListSourcesAsync(Guid movieId, CancellationToken cancellationToken = default) =>
        Task.FromResult(0);

    public static Task<int> CountTvShowMdbListSourcesAsync(Guid tvShowId, CancellationToken cancellationToken = default) =>
        Task.FromResult(0);
}
