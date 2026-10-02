using MovieApp.Application.Models.Movies;
using MovieApp.Application.Models.Recommendations;

namespace MovieApp.Application.Services.Recommendations;

public interface IRecommendationService
{
    Task<PaginatedResult<RecommendationItem>> GetSimilarMoviesAsync(
        Guid movieId,
        SimilarContentCriteria criteria,
        string contentLocale,
        CancellationToken cancellationToken = default);

    Task<PaginatedResult<RecommendationItem>> GetSimilarTvShowsAsync(
        Guid tvShowId,
        SimilarContentCriteria criteria,
        string contentLocale,
        CancellationToken cancellationToken = default);

    Task<PaginatedResult<RecommendationItem>> GetRecommendationsForCurrentUserAsync(
        RecommendationCriteria criteria,
        string contentLocale,
        CancellationToken cancellationToken = default);

    Task<IReadOnlyList<RecommendationSection>> GetHomeRecommendationsForCurrentUserAsync(
        bool includeColdStartDiscoverySections = true,
        string contentLocale = "en-US",
        RecommendationContentType contentType = RecommendationContentType.All,
        int? sectionItemCount = null,
        bool diversify = true,
        bool includeBecauseYouWatched = true,
        CancellationToken cancellationToken = default);
}
