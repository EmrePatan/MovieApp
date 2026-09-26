using MovieApp.Application.Models.AiRecommendations;

namespace MovieApp.Application.Abstractions.AiRecommendations;

public interface IAiMovieRecommendationValidator
{
    Task<AiValidationResult> ValidateAsync(
        Guid userId,
        IReadOnlyList<AiProviderSuggestion> suggestions,
        AiRecommendationSessionState session,
        int maxReturnedCount,
        string? searchLanguage = null,
        string? userMessage = null,
        CancellationToken cancellationToken = default);
}
