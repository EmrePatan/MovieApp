using MovieApp.Infrastructure.AiRecommendations;

namespace MovieApp.UnitTests.AiRecommendations;

public sealed class GroqStructuredOutputModeResolverTests
{
    [Theory]
    [InlineData("openai/gpt-oss-120b", true)]
    [InlineData("qwen/qwen3.8-27b", true)]
    [InlineData("llama-3.3-70b-versatile", false)]
    [InlineData("meta-llama/llama-4-scout-17b-16e-instruct", false)]
    public void Resolve_UsesJsonSchemaForSupportedModels(string modelId, bool expectsJsonSchema) =>
        Assert.Equal(
            expectsJsonSchema,
            GroqStructuredOutputModeResolver.Resolve(modelId) == OpenAiStructuredOutputMode.JsonSchemaBestEffort);
}
