using Microsoft.Extensions.Logging;

namespace MovieApp.Application.Services.Search;

internal static partial class AutocompleteServiceLogMessages
{
    [LoggerMessage(
        EventId = 1,
        Level = LogLevel.Warning,
        Message = "Autocomplete provider search failed for query {Query}; falling back to database.")]
    internal static partial void LogDbFallback(
        ILogger logger,
        string query,
        Exception exception);
}
