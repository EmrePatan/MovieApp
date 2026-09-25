using MovieApp.Application.Configuration;
using MovieApp.Application.Models.AiRecommendations;

namespace MovieApp.Application.Abstractions.AiRecommendations;

public interface IAiExternalLlmRecommendationProvider
{
    string ProviderName { get; }

    bool IsConfigured(AiRecommendationOptions options);

    Task<AiExternalLlmProviderAttempt> TryGenerateAsync(
        AiProviderRequest request,
        TimeSpan timeout,
        CancellationToken cancellationToken);
}
