using System.Net;
using Microsoft.Extensions.Logging;

namespace MovieApp.Infrastructure.AiRecommendations;

internal static partial class OpenAiCompatibleRecommendationProviderLogMessages
{
    [LoggerMessage(Level = LogLevel.Warning, Message = "{ProviderName} provider failed with status {StatusCode}. Body: {Body}")]
    public static partial void LogProviderFailure(
        ILogger logger,
        string providerName,
        HttpStatusCode statusCode,
        string body);
}
