using Microsoft.Extensions.Logging;

namespace MovieApp.Application.Services.Library;

internal static partial class LibraryServiceLogMessages
{
    [LoggerMessage(
        EventId = 4201,
        Level = LogLevel.Information,
        Message = "LibraryPaginationPerf Category={Category} Mode={Mode} CountExecuted={CountExecuted} CountMs={CountMs} PageFetchMs={PageFetchMs} TotalMs={TotalMs} PageSize={PageSize} ReturnedCount={ReturnedCount} HasNextPage={HasNextPage}")]
    public static partial void LogLibraryPaginationPerf(
        ILogger logger,
        int category,
        string mode,
        bool countExecuted,
        long countMs,
        long pageFetchMs,
        long totalMs,
        int pageSize,
        int returnedCount,
        bool hasNextPage);
}
