namespace MovieApp.Application.Services.Keywords;

public interface ICatalogKeywordIngestionService
{
    Task TryEnrichMovieKeywordsAsync(
        Guid movieId,
        bool refreshKeywords,
        CancellationToken cancellationToken = default);

    Task TryEnrichTvShowKeywordsAsync(
        Guid tvShowId,
        bool refreshKeywords,
        CancellationToken cancellationToken = default);
}
