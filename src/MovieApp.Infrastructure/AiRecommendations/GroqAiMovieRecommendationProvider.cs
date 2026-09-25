using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using MovieApp.Application.Abstractions.AiRecommendations;
using MovieApp.Application.Configuration;
using MovieApp.Application.Models.AiRecommendations;

namespace MovieApp.Infrastructure.AiRecommendations;

internal sealed class GroqAiMovieRecommendationProvider(
    HttpClient httpClient,
    IOptions<AiRecommendationOptions> options,
    ILogger<GroqAiMovieRecommendationProvider> logger) : IAiExternalLlmRecommendationProvider
{
    public string ProviderName => "Groq";

    public bool IsConfigured(AiRecommendationOptions settings) =>
        settings.Groq.Enabled &&
        !string.IsNullOrWhiteSpace(settings.Groq.ApiKey) &&
        !string.IsNullOrWhiteSpace(settings.Groq.ModelId);

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
        var groq = options.Value.Groq;
        var baseUrl = string.IsNullOrWhiteSpace(groq.BaseUrl)
            ? "https://api.groq.com/openai/v1"
            : groq.BaseUrl.TrimEnd('/');
        var endpoint = new Uri($"{baseUrl}/chat/completions");

        var client = new OpenAiCompatibleStructuredRecommendationClient(httpClient, logger);
        var (result, _, _) = await client.SendAsync(
            ProviderName,
            endpoint,
            groq.ApiKey,
            groq.ModelId,
            request,
            cancellationToken);

        return result;
    }
}
