using MovieApp.Application.Models.Search;

namespace MovieApp.Application.Abstractions.Persistence;

public interface IContentSearchTitleProviderEnrichmentRepository
{
    Task<int> CountMoviesWithTmdbIdAsync(CancellationToken cancellationToken = default);

    Task<int> CountTvShowsWithResolvableProviderIdAsync(CancellationToken cancellationToken = default);

    Task<IReadOnlyList<ContentSearchTitleEnrichmentCandidate>> SelectMovieCandidatesAsync(
        Guid? startAfterId,
        Guid? onlyMovieId,
        int take,
        CancellationToken cancellationToken = default);

    Task<IReadOnlyList<ContentSearchTitleEnrichmentCandidate>> SelectTvShowCandidatesAsync(
        Guid? startAfterId,
        Guid? onlyTvShowId,
        int take,
        CancellationToken cancellationToken = default);

    Task<Guid?> FindMovieIdByTitleAsync(string title, CancellationToken cancellationToken = default);

    Task<Guid?> FindMovieIdByTmdbIdAsync(int tmdbId, CancellationToken cancellationToken = default);
}
