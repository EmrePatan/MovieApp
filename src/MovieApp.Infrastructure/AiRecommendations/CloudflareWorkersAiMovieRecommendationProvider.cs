using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using MovieApp.Application.Abstractions.AiRecommendations;
using MovieApp.Application.Configuration;
using MovieApp.Application.Exceptions;
using MovieApp.Application.Models.AiRecommendations;
using MovieApp.Application.Services.AiRecommendations;

namespace MovieApp.Infrastructure.AiRecommendations;

internal sealed class CloudflareWorkersAiMovieRecommendationProvider(
    HttpClient httpClient,
    IOptions<AiRecommendationOptions> options,
    ILogger<CloudflareWorkersAiMovieRecommendationProvider> logger) : IAiExternalLlmRecommendationProvider
{
    public string ProviderName => "Cloudflare";

    public bool IsConfigured(AiRecommendationOptions settings) =>
        settings.Cloudflare.Enabled &&
        !string.IsNullOrWhiteSpace(settings.Cloudflare.AccountId) &&
        !string.IsNullOrWhiteSpace(settings.Cloudflare.ApiToken) &&
        !string.IsNullOrWhiteSpace(settings.Cloudflare.ModelId);

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
        var cloudflare = options.Value.Cloudflare;
        var endpoint =
            $"https://api.cloudflare.com/client/v4/accounts/{cloudflare.AccountId}/ai/run/{cloudflare.ModelId}";

        var systemInstruction = AiRecommendationPromptBuilder.BuildSystemInstruction(request.ResponseLanguage);
        var userPrompt = AiRecommendationPromptBuilder.BuildUserPrompt(request);
        var schemaHint = AiRecommendationPromptBuilder.BuildJsonSchemaDescription(request.SuggestionCount);

        var payload = new
        {
            messages = new object[]
            {
                new { role = "system", content = systemInstruction + "\n\nJSON schema:\n" + schemaHint },
                new { role = "user", content = userPrompt }
            }
        };

        using var httpRequest = new HttpRequestMessage(HttpMethod.Post, endpoint)
        {
            Content = new StringContent(JsonSerializer.Serialize(payload), Encoding.UTF8, "application/json")
        };
        httpRequest.Headers.Authorization = new AuthenticationHeaderValue("Bearer", cloudflare.ApiToken);

        using var response = await httpClient.SendAsync(httpRequest, cancellationToken);
        var responseBody = await response.Content.ReadAsStringAsync(cancellationToken);

        if (!response.IsSuccessStatusCode)
        {
            CloudflareWorkersAiRecommendationProviderLogMessages.LogProviderFailure(
                logger,
                response.StatusCode,
                TruncateBody(responseBody));
            throw new AiRecommendationProviderHttpException(response.StatusCode);
        }

        var structured = ExtractStructuredContent(responseBody);
        return AiStructuredRecommendationParser.Parse(structured);
    }

    private static string ExtractStructuredContent(string responseBody)
    {
        using var document = JsonDocument.Parse(responseBody);
        var root = document.RootElement;

        if (root.TryGetProperty("result", out var resultElement))
        {
            if (resultElement.ValueKind == JsonValueKind.String)
            {
                return resultElement.GetString() ?? string.Empty;
            }

            if (resultElement.TryGetProperty("response", out var responseElement) &&
                responseElement.ValueKind == JsonValueKind.String)
            {
                return responseElement.GetString() ?? string.Empty;
            }
        }

        throw new AiRecommendationProviderException("Cloudflare returned unexpected response shape.");
    }

    private static string TruncateBody(string body) =>
        body.Length <= 500 ? body : body[..500];
}

internal static partial class CloudflareWorkersAiRecommendationProviderLogMessages
{
    [LoggerMessage(Level = LogLevel.Warning, Message = "Cloudflare Workers AI provider failed with status {StatusCode}. Body: {Body}")]
    public static partial void LogProviderFailure(
        ILogger logger,
        System.Net.HttpStatusCode statusCode,
        string body);
}
