using Microsoft.Extensions.Logging;

namespace MovieApp.Application.Services.Search;

internal static partial class DiscoveryBatchLogMessages
{
    [LoggerMessage(
        EventId = 7311,
        Level = LogLevel.Warning,
        Message = "Genre cover browse provider unavailable for genre {GenreId}.")]
    public static partial void LogGenreCoverBrowseProviderUnavailable(
        ILogger logger,
        Guid genreId,
        Exception exception);

    [LoggerMessage(
        EventId = 7312,
        Level = LogLevel.Warning,
        Message = "Provider preview advanced discover unavailable for provider {ProviderId}.")]
    public static partial void LogProviderPreviewUnavailable(
        ILogger logger,
        int providerId,
        Exception exception);
}
