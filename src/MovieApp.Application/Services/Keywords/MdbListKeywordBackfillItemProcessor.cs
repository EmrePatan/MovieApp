using MovieApp.Application.Models.Keywords;

namespace MovieApp.Application.Services.Keywords;

public sealed class MdbListKeywordBackfillItemProcessor(
    IMdbListKeywordIngestionService ingestionService) : IMdbListKeywordBackfillItemProcessor
{
    public async Task<CatalogKeywordBackfillItemOutcome> ProcessAsync(
        CatalogKeywordBackfillCandidate candidate,
        CancellationToken cancellationToken = default)
    {
        var result = candidate.ContentType == "movie"
            ? await ingestionService.IngestMovieAsync(candidate.CatalogId, cancellationToken)
            : await ingestionService.IngestTvShowAsync(candidate.CatalogId, cancellationToken);

        return result.Status switch
        {
            MdbListKeywordIngestionStatus.Succeeded => CatalogKeywordBackfillItemOutcome.Succeeded,
            MdbListKeywordIngestionStatus.NotEligible => CatalogKeywordBackfillItemOutcome.Skipped,
            MdbListKeywordIngestionStatus.CatalogNotFound => CatalogKeywordBackfillItemOutcome.Skipped,
            _ => CatalogKeywordBackfillItemOutcome.Failed,
        };
    }
}
