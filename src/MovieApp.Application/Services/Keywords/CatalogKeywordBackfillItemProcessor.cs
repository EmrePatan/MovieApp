using MovieApp.Application.Abstractions.Persistence;
using MovieApp.Application.Models.Keywords;

namespace MovieApp.Application.Services.Keywords;

public sealed class CatalogKeywordBackfillItemProcessor(
    ICatalogKeywordBackfillRepository backfillRepository,
    ICatalogKeywordIngestionService keywordIngestionService) : ICatalogKeywordBackfillItemProcessor
{
    public Task<CatalogKeywordBackfillItemOutcome> ProcessAsync(
        CatalogKeywordBackfillCandidate candidate,
        CancellationToken cancellationToken = default) =>
        candidate.ContentType == "movie"
            ? ProcessMovieAsync(candidate.CatalogId, cancellationToken)
            : ProcessTvShowAsync(candidate.CatalogId, cancellationToken);

    private async Task<CatalogKeywordBackfillItemOutcome> ProcessMovieAsync(
        Guid movieId,
        CancellationToken cancellationToken)
    {
        if (await backfillRepository.IsMovieKeywordSyncedAsync(movieId, cancellationToken))
        {
            return CatalogKeywordBackfillItemOutcome.Skipped;
        }

        await keywordIngestionService.TryEnrichMovieKeywordsAsync(
            movieId,
            refreshKeywords: false,
            cancellationToken);

        return await backfillRepository.IsMovieKeywordSyncedAsync(movieId, cancellationToken)
            ? CatalogKeywordBackfillItemOutcome.Succeeded
            : CatalogKeywordBackfillItemOutcome.Failed;
    }

    private async Task<CatalogKeywordBackfillItemOutcome> ProcessTvShowAsync(
        Guid tvShowId,
        CancellationToken cancellationToken)
    {
        if (await backfillRepository.IsTvShowKeywordSyncedAsync(tvShowId, cancellationToken))
        {
            return CatalogKeywordBackfillItemOutcome.Skipped;
        }

        await keywordIngestionService.TryEnrichTvShowKeywordsAsync(
            tvShowId,
            refreshKeywords: false,
            cancellationToken);

        return await backfillRepository.IsTvShowKeywordSyncedAsync(tvShowId, cancellationToken)
            ? CatalogKeywordBackfillItemOutcome.Succeeded
            : CatalogKeywordBackfillItemOutcome.Failed;
    }
}
