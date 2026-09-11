using MovieApp.Application.Models.Recommendations;

namespace MovieApp.Application.Abstractions.Persistence;

public interface IRecommendationRepository
{
    Task<bool> MovieExistsAsync(Guid movieId, CancellationToken cancellationToken = default);

    Task<bool> TvShowExistsAsync(Guid tvShowId, CancellationToken cancellationToken = default);

    Task<SimilaritySourceProfile?> GetMovieSimilarityProfileAsync(
        Guid movieId,
        CancellationToken cancellationToken = default);

    Task<SimilaritySourceProfile?> GetTvShowSimilarityProfileAsync(
        Guid tvShowId,
        CancellationToken cancellationToken = default);

    Task<IReadOnlyList<SimilarityCandidateProfile>> GetSimilarMovieCandidatesAsync(
        Guid sourceMovieId,
        IReadOnlyList<Guid> genreIds,
        int maxCandidates,
        CancellationToken cancellationToken = default);

    Task<IReadOnlyList<SimilarityCandidateProfile>> GetSimilarTvShowCandidatesAsync(
        Guid sourceTvShowId,
        IReadOnlyList<Guid> genreIds,
        int maxCandidates,
        CancellationToken cancellationToken = default);

    Task<UserRecommendationContext> GetUserRecommendationContextAsync(
        Guid userId,
        CancellationToken cancellationToken = default);

    Task<IReadOnlyList<PersonalizedCandidateProfile>> GetPersonalizedCandidatesAsync(
        RecommendationContentType type,
        IReadOnlyList<Guid> preferredGenreIds,
        IReadOnlySet<Guid> excludedMovieIds,
        IReadOnlySet<Guid> excludedTvShowIds,
        int maxCandidates,
        CancellationToken cancellationToken = default);
}
