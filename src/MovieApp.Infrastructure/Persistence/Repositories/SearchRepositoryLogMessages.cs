using Microsoft.Extensions.Logging;

namespace MovieApp.Infrastructure.Persistence.Repositories;

internal static partial class SearchRepositoryLogMessages
{
    [LoggerMessage(
        EventId = 4101,
        Level = LogLevel.Information,
        Message = "SearchPaginationPerf Mode={Mode} CountExecuted={CountExecuted} CountMs={CountMs} PageFetchMs={PageFetchMs} TotalMs={TotalMs} PageSize={PageSize} ReturnedCount={ReturnedCount} HasNextPage={HasNextPage}")]
    public static partial void LogSearchPaginationPerf(
        ILogger logger,
        string mode,
        bool countExecuted,
        long countMs,
        long pageFetchMs,
        long totalMs,
        int pageSize,
        int returnedCount,
        bool hasNextPage);
}
