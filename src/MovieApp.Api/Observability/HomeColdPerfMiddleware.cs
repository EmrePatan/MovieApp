using System.Diagnostics;
using Microsoft.Extensions.Logging;
using MovieApp.Infrastructure.Performance;

namespace MovieApp.Api.Observability;

internal sealed partial class HomeColdPerfMiddleware(
    RequestDelegate next,
    ILogger<HomeColdPerfMiddleware> logger)
{
    public async Task InvokeAsync(HttpContext context)
    {
        if (!TryResolveOperation(context.Request, out var operation, out var route))
        {
            await next(context);
            return;
        }

        using var perfScope = HomeColdPerfScope.Begin(operation);
        var stopwatch = Stopwatch.StartNew();
        await next(context);
        stopwatch.Stop();

        var metrics = perfScope.Metrics;
        LogHomeColdPerfRequest(
            logger,
            metrics.Operation,
            route,
            context.Response.StatusCode,
            stopwatch.ElapsedMilliseconds,
            metrics.CommandCount,
            metrics.PeakActiveCommands,
            metrics.CommandExecutionMs,
            metrics.ConnectionOpenMs);
    }

    private static bool TryResolveOperation(HttpRequest request, out string operation, out string route)
    {
        if (!request.Method.Equals(HttpMethods.Get, StringComparison.OrdinalIgnoreCase))
        {
            operation = string.Empty;
            route = string.Empty;
            return false;
        }

        var path = request.Path.Value ?? string.Empty;
        if (path.Equals("/api/home/browse", StringComparison.OrdinalIgnoreCase))
        {
            operation = "HomeBrowse";
            route = path;
            return true;
        }

        if (path.Equals("/api/home/personalized", StringComparison.OrdinalIgnoreCase))
        {
            operation = "HomePersonalized";
            route = path;
            return true;
        }

        if (path.Equals("/api/catalog/upcoming", StringComparison.OrdinalIgnoreCase))
        {
            operation = "CatalogUpcoming";
            route = path;
            return true;
        }

        operation = string.Empty;
        route = string.Empty;
        return false;
    }

    [LoggerMessage(
        EventId = 7300,
        Level = LogLevel.Information,
        Message = "HomeColdPerf Request Operation={Operation} Route={Route} StatusCode={StatusCode} HttpTotalMs={HttpTotalMs} CommandCount={CommandCount} PeakActiveCommands={PeakActiveCommands} CommandExecutionMs={CommandExecutionMs} ConnectionOpenMs={ConnectionOpenMs}")]
    private static partial void LogHomeColdPerfRequest(
        ILogger logger,
        string operation,
        string route,
        int statusCode,
        long httpTotalMs,
        int commandCount,
        int peakActiveCommands,
        long commandExecutionMs,
        long connectionOpenMs);
}
