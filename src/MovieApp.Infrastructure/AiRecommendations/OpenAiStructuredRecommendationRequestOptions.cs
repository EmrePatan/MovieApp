namespace MovieApp.Infrastructure.AiRecommendations;

internal enum OpenAiStructuredOutputMode
{
    JsonObject,
    JsonSchemaBestEffort
}

internal sealed record OpenAiStructuredRecommendationRequestOptions(
    OpenAiStructuredOutputMode OutputMode = OpenAiStructuredOutputMode.JsonObject,
    double Temperature = 0.35);
