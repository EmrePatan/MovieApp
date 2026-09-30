using MovieApp.Application.Models.Keywords;

namespace MovieApp.Application.Services.Keywords;

public interface IMdbListKeywordIngestionService
{
    Task<MdbListKeywordIngestionResult> IngestMovieAsync(
        Guid movieId,
        CancellationToken cancellationToken = default);

    Task<MdbListKeywordIngestionResult> IngestTvShowAsync(
        Guid tvShowId,
        CancellationToken cancellationToken = default);

    Task<MdbListKeywordIngestionResult> IngestMovieByTmdbIdAsync(
        int tmdbId,
        CancellationToken cancellationToken = default);

    Task<MdbListKeywordIngestionResult> IngestTvShowByTmdbIdAsync(
        int tmdbId,
        CancellationToken cancellationToken = default);

    Task<MdbListKeywordIngestionResult> IngestMovieWithProviderKeywordsAsync(
        Guid movieId,
        IReadOnlyList<MdbListKeywordTransportItem> providerKeywords,
        CancellationToken cancellationToken = default);

    Task<MdbListKeywordIngestionResult> IngestTvShowWithProviderKeywordsAsync(
        Guid tvShowId,
        IReadOnlyList<MdbListKeywordTransportItem> providerKeywords,
        CancellationToken cancellationToken = default);
}
