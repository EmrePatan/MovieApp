using MovieApp.Application.Models.AiRecommendations;

namespace MovieApp.Application.Abstractions.AiRecommendations;

public interface IAiMovieRecommendationProvider
{
    Task<AiProviderGenerationResult> GenerateAsync(
        AiProviderRequest request,
        CancellationToken cancellationToken = default);
}
