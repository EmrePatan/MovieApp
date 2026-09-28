using Microsoft.Extensions.Logging;

namespace MovieApp.Infrastructure.AiRecommendations;

internal static partial class GroqAiMovieRecommendationProviderLogMessages
{
    [LoggerMessage(
        Level = LogLevel.Information,
        Message = "Groq json_schema request failed for model {ModelId}; retrying with json_object.")]
    public static partial void LogJsonSchemaFallback(ILogger logger, string modelId);

    [LoggerMessage(
        Level = LogLevel.Information,
        Message = "Groq structured JSON request failed for model {ModelId}; retrying with lower temperature json_object.")]
    public static partial void LogJsonObjectRetry(ILogger logger, string modelId);
}
