using MovieApp.Application.Models.Movies;
using MovieApp.Application.Models.Recommendations;

namespace MovieApp.Application.Services.Recommendations;

public interface IRecommendationService
{
    Task<PaginatedResult<RecommendationItem>> GetSimilarMoviesAsync(
        Guid movieId,
        SimilarContentCriteria criteria,
        CancellationToken cancellationToken = default);

    Task<PaginatedResult<RecommendationItem>> GetSimilarTvShowsAsync(
        Guid tvShowId,
        SimilarContentCriteria criteria,
        CancellationToken cancellationToken = default);

    Task<PaginatedResult<RecommendationItem>> GetRecommendationsForCurrentUserAsync(
        RecommendationCriteria criteria,
        CancellationToken cancellationToken = default);

    Task<IReadOnlyList<RecommendationSection>> GetHomeRecommendationsForCurrentUserAsync(
        bool includeColdStartDiscoverySections = true,
        CancellationToken cancellationToken = default);
}
