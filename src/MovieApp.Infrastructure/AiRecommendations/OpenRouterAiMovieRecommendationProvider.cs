using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using MovieApp.Application.Abstractions.AiRecommendations;
using MovieApp.Application.Configuration;
using MovieApp.Application.Models.AiRecommendations;

namespace MovieApp.Infrastructure.AiRecommendations;

internal sealed class OpenRouterAiMovieRecommendationProvider(
    HttpClient httpClient,
    IOptions<AiRecommendationOptions> options,
    ILogger<OpenRouterAiMovieRecommendationProvider> logger) : IAiExternalLlmRecommendationProvider
{
    public string ProviderName => "OpenRouter";

    public bool IsConfigured(AiRecommendationOptions settings) =>
        settings.OpenRouter.Enabled &&
        !string.IsNullOrWhiteSpace(settings.OpenRouter.ApiKey) &&
        !string.IsNullOrWhiteSpace(settings.OpenRouter.ModelId);

    public Task<AiExternalLlmProviderAttempt> TryGenerateAsync(
        AiProviderRequest request,
        TimeSpan timeout,
        CancellationToken cancellationToken) =>
        ExternalLlmRecommendationAttemptExecutor.ExecuteAsync(
            ProviderName,
            token => GenerateInternalAsync(request, token),
            timeout,
            cancellationToken);

    private async Task<AiProviderGenerationResult> GenerateInternalAsync(
        AiProviderRequest request,
        CancellationToken cancellationToken)
    {
        var openRouter = options.Value.OpenRouter;
        var baseUrl = string.IsNullOrWhiteSpace(openRouter.BaseUrl)
            ? "https://openrouter.ai/api/v1"
            : openRouter.BaseUrl.TrimEnd('/');
        var endpoint = new Uri($"{baseUrl}/chat/completions");

        var client = new OpenAiCompatibleStructuredRecommendationClient(httpClient, logger);
        var (result, _, _) = await client.SendAsync(
            ProviderName,
            endpoint,
            openRouter.ApiKey,
            openRouter.ModelId,
            request,
            cancellationToken);

        return result;
    }
}
