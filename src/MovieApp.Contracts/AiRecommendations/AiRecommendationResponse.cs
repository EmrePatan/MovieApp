namespace MovieApp.Contracts.AiRecommendations;

public sealed record AiRecommendationResponse(
    Guid SessionId,
    bool IsAiGenerated,
    bool PartialResults,
    int RequestedCount,
    int ReturnedCount,
    int QuotaRemaining,
    IReadOnlyList<AiRecommendationMovieItemResponse> Recommendations,
    AiRecommendationValidationSummaryResponse ValidationSummary);
