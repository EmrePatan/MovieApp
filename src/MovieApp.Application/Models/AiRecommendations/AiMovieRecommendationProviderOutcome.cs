namespace MovieApp.Application.Models.AiRecommendations;

public sealed record AiMovieRecommendationProviderOutcome(
    AiProviderGenerationResult Generation,
    bool IsAiGenerated,
    string SelectedSource);
