using MovieApp.Application.Models.AiRecommendations;

namespace MovieApp.Application.Abstractions.AiRecommendations;

public interface IDeterministicAiMovieRecommendationProvider
{
    Task<AiProviderGenerationResult> GenerateAsync(
        AiProviderRequest request,
        CancellationToken cancellationToken = default);
}
