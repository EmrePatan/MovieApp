using MovieApp.Domain.Enums;

namespace MovieApp.Application.Abstractions.Persistence;

public sealed record ContentLocalizedPosterBackfillCandidate(
    CatalogContentType ContentType,
    Guid ContentId,
    int TmdbId);

public interface IContentLocalizedPosterBackfillRepository
{
    Task<IReadOnlyList<ContentLocalizedPosterBackfillCandidate>> SelectCandidatesAsync(
        int batchSize,
        Guid? startAfterMovieId,
        Guid? startAfterTvShowId,
        CancellationToken cancellationToken = default);
}
