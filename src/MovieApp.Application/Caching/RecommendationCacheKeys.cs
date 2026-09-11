using MovieApp.Application.Models.Recommendations;
using MovieApp.Application.Recommendations;

namespace MovieApp.Application.Caching;

public static class RecommendationCacheKeys
{
    public const string SimilarMoviePrefix = "recommendation-similar-movie:";

    public const string SimilarTvPrefix = "recommendation-similar-tv:";

    public const string UserPrefix = "recommendation-user:";

    public const string HomePrefix = "recommendation-home:";

    public static string SimilarMovie(Guid movieId, int page, int pageSize) =>
        $"{SimilarMoviePrefix}{movieId}:page:{page}:size:{pageSize}:{RecommendationAlgorithmVersion.Current}";

    public static string SimilarTv(Guid tvShowId, int page, int pageSize) =>
        $"{SimilarTvPrefix}{tvShowId}:page:{page}:size:{pageSize}:{RecommendationAlgorithmVersion.Current}";

    public static string User(Guid userId, RecommendationContentType type, int page, int pageSize) =>
        $"{UserPrefix}{userId}:{type}:{page}:{pageSize}:{RecommendationAlgorithmVersion.Current}";

    public static string Home(Guid userId) =>
        $"{HomePrefix}{userId}:{RecommendationAlgorithmVersion.Current}";
}
