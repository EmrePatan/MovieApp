using Microsoft.Extensions.Logging;

namespace MovieApp.Application.Services.CatalogFollows;

internal static partial class CatalogUpcomingPerfLogMessages
{
    [LoggerMessage(
        EventId = 8301,
        Level = LogLevel.Debug,
        Message = "CatalogUpcomingPerf Scope={Scope} TotalMs={TotalMs} RepositoryMs={RepositoryMs} Page={Page} PageSize={PageSize} TotalCount={TotalCount} ItemCount={ItemCount} IsAuthenticated={IsAuthenticated}")]
    public static partial void LogRequest(
        ILogger logger,
        string scope,
        long totalMs,
        long repositoryMs,
        int page,
        int pageSize,
        int totalCount,
        int itemCount,
        bool isAuthenticated);
}
