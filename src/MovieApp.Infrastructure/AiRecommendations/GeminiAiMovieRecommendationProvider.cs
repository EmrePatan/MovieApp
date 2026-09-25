using System.Diagnostics;
using System.Net;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using MovieApp.Application.Abstractions.AiRecommendations;
using MovieApp.Application.Configuration;
using MovieApp.Application.Exceptions;
using MovieApp.Application.Models.AiRecommendations;
using MovieApp.Application.Services.AiRecommendations;

namespace MovieApp.Infrastructure.AiRecommendations;

internal sealed class GeminiAiMovieRecommendationProvider(
    HttpClient httpClient,
    IOptions<AiRecommendationOptions> options,
    ILogger<GeminiAiMovieRecommendationProvider> logger) : IAiExternalLlmRecommendationProvider
{
    public string ProviderName => "Gemini";

    private static readonly JsonSerializerOptions SerializerOptions = new(JsonSerializerDefaults.Web)
    {
        PropertyNameCaseInsensitive = true,
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull
    };

    public bool IsConfigured(AiRecommendationOptions settings) =>
        settings.Gemini.Enabled && !string.IsNullOrWhiteSpace(settings.Gemini.ApiKey);

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
        var gemini = options.Value.Gemini;
        var modelId = string.IsNullOrWhiteSpace(gemini.ModelId)
            ? "gemini-3.1-flash-lite"
            : gemini.ModelId.Trim();

        var endpoint =
            $"https://generativelanguage.googleapis.com/v1beta/models/{modelId}:generateContent?key={Uri.EscapeDataString(gemini.ApiKey)}";

        using var httpRequest = new HttpRequestMessage(HttpMethod.Post, endpoint)
        {
            Content = new StringContent(BuildRequestBody(request), Encoding.UTF8, "application/json")
        };

        using var response = await httpClient.SendAsync(httpRequest, cancellationToken);
        var responseBody = await response.Content.ReadAsStringAsync(cancellationToken);

        if (!response.IsSuccessStatusCode)
        {
            GeminiAiMovieRecommendationProviderLogMessages.LogProviderFailure(
                logger,
                response.StatusCode,
                TruncateBody(responseBody));
            throw new AiRecommendationProviderHttpException(response.StatusCode);
        }

        return ParseGeminiResponse(responseBody);
    }

    internal static string BuildRequestBody(AiProviderRequest request)
    {
        var prompt = AiRecommendationPromptBuilder.BuildUserPrompt(request);
        var payload = new
        {
            systemInstruction = new
            {
                parts = new[]
                {
                    new
                    {
                        text = AiRecommendationPromptBuilder.BuildSystemInstruction(request.ResponseLanguage)
                    }
                }
            },
            contents = new[]
            {
                new
                {
                    role = "user",
                    parts = new[] { new { text = prompt } }
                }
            },
            generationConfig = new
            {
                responseMimeType = "application/json",
                temperature = 0.7,
                responseSchema = new
                {
                    type = "object",
                    properties = new
                    {
                        suggestions = new
                        {
                            type = "array",
                            maxItems = request.SuggestionCount,
                            items = new
                            {
                                type = "object",
                                properties = new
                                {
                                    title = new { type = "string" },
                                    year = new { type = "integer" },
                                    mediaType = new { type = "string" },
                                    tmdbId = new { type = "integer" },
                                    reason = new { type = "string" }
                                },
                                required = new[] { "title", "year", "mediaType", "reason" }
                            }
                        },
                        constraintUpdates = new
                        {
                            type = "object",
                            properties = new
                            {
                                desiredGenres = new { type = "array", items = new { type = "string" } },
                                excludedGenres = new { type = "array", items = new { type = "string" } },
                                maxRuntimeMinutes = new { type = "integer" },
                                minYear = new { type = "integer" },
                                maxYear = new { type = "integer" },
                                moodKeywords = new { type = "array", items = new { type = "string" } }
                            }
                        }
                    },
                    required = new[] { "suggestions" }
                }
            }
        };

        return JsonSerializer.Serialize(payload, SerializerOptions);
    }

    internal static AiProviderGenerationResult ParseGeminiResponse(string responseBody)
    {
        using var document = JsonDocument.Parse(responseBody);
        var text = document.RootElement
            .GetProperty("candidates")[0]
            .GetProperty("content")
            .GetProperty("parts")[0]
            .GetProperty("text")
            .GetString();

        if (string.IsNullOrWhiteSpace(text))
        {
            throw new AiRecommendationProviderException("Gemini returned empty content.");
        }

        return AiStructuredRecommendationParser.Parse(text);
    }

    internal static AiProviderGenerationResult ParseResponse(string responseBody) =>
        ParseGeminiResponse(responseBody);

    private static string TruncateBody(string body) =>
        body.Length <= 500 ? body : body[..500];
}

internal static partial class GeminiAiMovieRecommendationProviderLogMessages
{
    [LoggerMessage(Level = LogLevel.Warning, Message = "Gemini provider failed with status {StatusCode}. Body: {Body}")]
    public static partial void LogProviderFailure(ILogger logger, HttpStatusCode statusCode, string body);
}
