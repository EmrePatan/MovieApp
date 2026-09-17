namespace MovieApp.Contracts.AiRecommendations;

public sealed record AiRecommendationValidationSummaryResponse(
    int GeminiSuggestionCount,
    int ValidatedCount,
    int RejectedCount);
