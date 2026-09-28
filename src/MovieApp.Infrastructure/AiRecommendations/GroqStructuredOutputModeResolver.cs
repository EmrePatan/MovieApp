namespace MovieApp.Infrastructure.AiRecommendations;

internal static class GroqStructuredOutputModeResolver
{
    public static OpenAiStructuredOutputMode Resolve(string modelId)
    {
        if (string.IsNullOrWhiteSpace(modelId))
        {
            return OpenAiStructuredOutputMode.JsonObject;
        }

        var normalized = modelId.Trim().ToLowerInvariant();
        if (normalized.Contains("gpt-oss", StringComparison.Ordinal) ||
            normalized.Contains("qwen", StringComparison.Ordinal))
        {
            return OpenAiStructuredOutputMode.JsonSchemaBestEffort;
        }

        return OpenAiStructuredOutputMode.JsonObject;
    }
}
