using MovieApp.Application.Models.AiRecommendations;
using MovieApp.Contracts.AiRecommendations;

namespace MovieApp.Api.Mapping;

public static class AiRecommendationContractMapper
{
    public static AiRecommendationResponse ToResponse(AiRecommendationServiceResult result) =>
        new(
            result.SessionId,
            result.IsAiGenerated,
            result.PartialResults,
            result.RequestedCount,
            result.ReturnedCount,
            result.QuotaRemaining,
            result.Recommendations.Select(ToMovieItem).ToList(),
            new AiRecommendationValidationSummaryResponse(
                result.ValidationSummary.GeminiSuggestionCount,
                result.ValidationSummary.ValidatedCount,
                result.ValidationSummary.RejectedCount));

    private static AiRecommendationMovieItemResponse ToMovieItem(AiValidatedRecommendation recommendation) =>
        new(
            recommendation.Movie.MovieId,
            "movie",
            recommendation.Movie.Title,
            recommendation.Movie.OriginalTitle,
            recommendation.Movie.Overview,
            recommendation.Movie.PosterUrl,
            recommendation.Movie.BackdropUrl,
            recommendation.Movie.ReleaseDate,
            recommendation.Movie.VoteAverage,
            recommendation.Movie.VoteCount,
            recommendation.Movie.Year,
            recommendation.Movie.RuntimeMinutes,
            recommendation.Reason);
}
