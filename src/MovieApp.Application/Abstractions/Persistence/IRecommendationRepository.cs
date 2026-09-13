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

    Task<IReadOnlyDictionary<Guid, SimilaritySourceProfile>> GetMovieSimilarityProfilesAsync(
        IReadOnlyList<Guid> movieIds,
        CancellationToken cancellationToken = default);

    Task<IReadOnlyDictionary<Guid, SimilaritySourceProfile>> GetTvShowSimilarityProfilesAsync(
        IReadOnlyList<Guid> tvShowIds,
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

    Task<IReadOnlyList<Guid>> GetSimilarMovieCandidateIdsAsync(
        Guid sourceMovieId,
        IReadOnlyList<Guid> genreIds,
        int maxCandidates,
        CancellationToken cancellationToken = default);

    Task<IReadOnlyList<Guid>> GetSimilarTvShowCandidateIdsAsync(
        Guid sourceTvShowId,
        IReadOnlyList<Guid> genreIds,
        int maxCandidates,
        CancellationToken cancellationToken = default);

    Task<IReadOnlyDictionary<Guid, IReadOnlyList<Guid>>> GetSimilarMovieCandidateIdsForSourcesAsync(
        IReadOnlyList<SimilaritySourceGenreRequest> sources,
        int maxCandidates,
        CancellationToken cancellationToken = default);

    Task<IReadOnlyDictionary<Guid, IReadOnlyList<Guid>>> GetSimilarTvShowCandidateIdsForSourcesAsync(
        IReadOnlyList<SimilaritySourceGenreRequest> sources,
        int maxCandidates,
        CancellationToken cancellationToken = default);

    Task<IReadOnlyList<SimilarityCandidateProfile>> GetSimilarMovieCandidatesByIdsAsync(
        IReadOnlyList<Guid> movieIds,
        CancellationToken cancellationToken = default);

    Task<IReadOnlyList<SimilarityCandidateProfile>> GetSimilarTvShowCandidatesByIdsAsync(
        IReadOnlyList<Guid> tvShowIds,
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
