namespace MovieApp.Application.Models.AiRecommendations;

public sealed record AiExternalLlmProviderAttempt(
    string ProviderName,
    bool Succeeded,
    AiProviderGenerationResult? Result,
    AiProviderFailureCategory? FailureCategory,
    int? HttpStatusCode,
    long DurationMs);
