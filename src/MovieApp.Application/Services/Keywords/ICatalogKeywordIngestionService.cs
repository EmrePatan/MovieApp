namespace MovieApp.Application.Services.Keywords;

public interface ICatalogKeywordIngestionService
{
    Task TryEnrichMovieKeywordsAsync(
        Guid movieId,
        bool refreshKeywords,
        IReadOnlyList<Models.Providers.ProviderKeywordSummary>? prefetchedKeywords = null,
        CancellationToken cancellationToken = default);

    Task TryEnrichTvShowKeywordsAsync(
        Guid tvShowId,
        bool refreshKeywords,
        IReadOnlyList<Models.Providers.ProviderKeywordSummary>? prefetchedKeywords = null,
        CancellationToken cancellationToken = default);
}
