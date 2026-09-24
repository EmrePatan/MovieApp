using MovieApp.Application.Models.Recommendations;
using MovieApp.Application.Recommendations;

namespace MovieApp.Application.Caching;

public static class RecommendationCacheKeys
{
    public const string SimilarMoviePrefix = "recommendation-similar-movie:";

    public const string SimilarTvPrefix = "recommendation-similar-tv:";

    public const string UserPrefix = "recommendation-user:";

    public const string HomePrefix = "recommendation-home:";

    public static string SimilarMovie(Guid movieId, int page, int pageSize, string contentLocale) =>
        ContentLocaleCacheKeySegment.Append(SimilarMovie(movieId, page, pageSize), contentLocale);

    public static string SimilarMovie(Guid movieId, int page, int pageSize) =>
        $"{SimilarMoviePrefix}{movieId}:page:{page}:size:{pageSize}:{RecommendationAlgorithmVersion.Similar}";

    public static string SimilarTv(Guid tvShowId, int page, int pageSize, string contentLocale) =>
        ContentLocaleCacheKeySegment.Append(SimilarTv(tvShowId, page, pageSize), contentLocale);

    public static string SimilarTv(Guid tvShowId, int page, int pageSize) =>
        $"{SimilarTvPrefix}{tvShowId}:page:{page}:size:{pageSize}:{RecommendationAlgorithmVersion.Similar}";

    public const string GenerationPrefix = "recommendation-gen:";

    public static string Generation(Guid userId) => $"{GenerationPrefix}{userId:N}";

    public static string User(
        Guid userId,
        RecommendationContentType type,
        int page,
        int pageSize,
        string contentLocale,
        long generation) =>
        ContentLocaleCacheKeySegment.Append(User(userId, type, page, pageSize, generation), contentLocale);

    public static string User(
        Guid userId,
        RecommendationContentType type,
        int page,
        int pageSize,
        long generation = 0) =>
        $"{UserPrefix}{userId}:{type}:{page}:{pageSize}:{RecommendationAlgorithmVersion.Personalized}:g{generation}";

    public static string Home(Guid userId, string contentLocale, long generation) =>
        ContentLocaleCacheKeySegment.Append(Home(userId, generation), contentLocale);

    public static string Home(Guid userId, long generation = 0) =>
        $"{HomePrefix}{userId}:{RecommendationAlgorithmVersion.Personalized}:g{generation}";
}
