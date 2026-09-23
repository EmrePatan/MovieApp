using Microsoft.Extensions.Logging;

namespace MovieApp.Infrastructure.Providers.AzureTranslator;

internal static partial class AzureReviewTranslationProviderLogMessages
{
    [LoggerMessage(
        EventId = 6310,
        Level = LogLevel.Warning,
        Message = "Azure Translator request failed with status code {StatusCode}.")]
    public static partial void LogProviderFailure(ILogger logger, int statusCode);
}
