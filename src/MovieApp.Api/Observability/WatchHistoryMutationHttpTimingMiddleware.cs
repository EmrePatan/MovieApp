using System.Diagnostics;
using Microsoft.Extensions.Logging;

namespace MovieApp.Api.Observability;

internal sealed partial class WatchHistoryMutationHttpTimingMiddleware(
    RequestDelegate next,
    ILogger<WatchHistoryMutationHttpTimingMiddleware> logger)
{
    public async Task InvokeAsync(HttpContext context)
    {
        if (!IsWatchHistoryMutation(context.Request))
        {
            await next(context);
            return;
        }

        var stopwatch = Stopwatch.StartNew();
        await next(context);
        stopwatch.Stop();

        var correlationId = CorrelationIdAccessor.Get(context) ?? context.TraceIdentifier;
        var catalogId = TryGetCatalogIdFromPath(context.Request.Path);

        LogWatchHistoryMutationHttp(
            logger,
            context.Request.Method,
            context.Request.Path.Value ?? string.Empty,
            context.Response.StatusCode,
            correlationId,
            catalogId,
            stopwatch.ElapsedMilliseconds);
    }

    private static bool IsWatchHistoryMutation(HttpRequest request) =>
        request.Method.Equals(HttpMethods.Post, StringComparison.OrdinalIgnoreCase) &&
        request.Path.StartsWithSegments("/api/watch-history", StringComparison.OrdinalIgnoreCase) &&
        (request.Path.Value?.Contains("/watch-state", StringComparison.OrdinalIgnoreCase) == true ||
         request.Path.Value?.Contains("/movies/", StringComparison.OrdinalIgnoreCase) == true);

    private static string? TryGetCatalogIdFromPath(PathString path)
    {
        var segments = path.Value?.Split('/', StringSplitOptions.RemoveEmptyEntries);
        if (segments is null || segments.Length < 4)
        {
            return null;
        }

        // api/watch-history/movies/{id} or api/watch-history/tvshows/{id}/...
        if (segments.Length >= 4 &&
            segments[0].Equals("api", StringComparison.OrdinalIgnoreCase) &&
            segments[1].Equals("watch-history", StringComparison.OrdinalIgnoreCase))
        {
            return segments[3];
        }

        return null;
    }

    [LoggerMessage(
        EventId = 7101,
        Level = LogLevel.Information,
        Message = "WatchHistoryPerf MutationHttp Method={Method} Path={Path} StatusCode={StatusCode} CorrelationId={CorrelationId} CatalogId={CatalogId} HttpTotalMs={HttpTotalMs}")]
    private static partial void LogWatchHistoryMutationHttp(
        ILogger logger,
        string method,
        string path,
        int statusCode,
        string correlationId,
        string? catalogId,
        long httpTotalMs);
}
