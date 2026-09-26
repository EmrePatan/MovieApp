using System.Diagnostics;
using Microsoft.Extensions.Logging;
using MovieApp.Infrastructure.Performance;
using StackExchange.Redis;

namespace MovieApp.Api.Observability;

internal sealed partial class HomeColdPerfMiddleware(
    RequestDelegate next,
    ILogger<HomeColdPerfMiddleware> logger,
    IConnectionMultiplexer? connectionMultiplexer = null)
{
    public async Task InvokeAsync(HttpContext context)
    {
        if (!TryResolveOperation(context.Request, out var operation, out var route))
        {
            await next(context);
            return;
        }

        var correlationId = CorrelationIdAccessor.Get(context) ?? context.TraceIdentifier;
        using var perfScope = HomeColdPerfScope.Begin(operation, correlationId);
        var stopwatch = Stopwatch.StartNew();
        await next(context);
        stopwatch.Stop();

        if (!logger.IsEnabled(LogLevel.Information))
        {
            return;
        }

        var runtimeAfter = RuntimePerfSnapshot.Capture();
        var metrics = perfScope.Metrics;
        LogHomeColdPerfRequest(
            logger,
            metrics.Operation,
            route,
            correlationId,
            context.Response.StatusCode,
            stopwatch.ElapsedMilliseconds,
            metrics.CommandCount,
            metrics.PeakActiveCommands,
            metrics.CommandExecutionMs,
            metrics.ConnectionOpenMs);

        LogRedisPerf(
            logger,
            metrics.Operation,
            correlationId,
            metrics.RedisGetCount,
            metrics.RedisGetTransportMs,
            metrics.RedisDeserializeMs,
            metrics.RedisMaxSingleGetTransportMs,
            metrics.RedisSetCount,
            metrics.RedisSerializeMs,
            metrics.RedisSetTransportMs);

        var mux = RedisMultiplexerPerfSnapshot.TryCapture(connectionMultiplexer);
        if (mux is not null)
        {
            LogRedisMuxPerf(
                logger,
                metrics.Operation,
                correlationId,
                mux.Value.IsConnected,
                mux.Value.IsConnecting,
                mux.Value.TotalOutstanding,
                mux.Value.InteractiveAwaitingResponse,
                mux.Value.InteractiveCompletedAsynchronously,
                mux.Value.InteractiveFailedAsynchronously);
        }

        LogRuntimePerf(
            logger,
            metrics.Operation,
            correlationId,
            runtimeAfter.ThreadPoolThreadCount,
            runtimeAfter.WorkerAvailable,
            runtimeAfter.WorkerMax,
            runtimeAfter.PendingWorkItemCount,
            runtimeAfter.GcHeapMb,
            runtimeAfter.Gen2Collections,
            runtimeAfter.WorkingSetMb,
            0);
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

        if (path.Equals("/api/auth/me", StringComparison.OrdinalIgnoreCase))
        {
            operation = "AuthMe";
            route = path;
            return true;
        }

        if (path.Equals("/api/notifications/unread-count", StringComparison.OrdinalIgnoreCase))
        {
            operation = "NotificationsUnreadCount";
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
        Message = "HomeColdPerf Request Operation={Operation} Route={Route} CorrelationId={CorrelationId} StatusCode={StatusCode} HttpTotalMs={HttpTotalMs} CommandCount={CommandCount} PeakActiveCommands={PeakActiveCommands} CommandExecutionMs={CommandExecutionMs} ConnectionOpenMs={ConnectionOpenMs}")]
    private static partial void LogHomeColdPerfRequest(
        ILogger logger,
        string operation,
        string route,
        string correlationId,
        int statusCode,
        long httpTotalMs,
        int commandCount,
        int peakActiveCommands,
        long commandExecutionMs,
        long connectionOpenMs);

    [LoggerMessage(
        EventId = 7301,
        Level = LogLevel.Information,
        Message = "RedisPerf Operation={Operation} CorrelationId={CorrelationId} GetCount={GetCount} GetTransportMs={GetTransportMs} DeserializeMs={DeserializeMs} MaxSingleGetTransportMs={MaxSingleGetTransportMs} SetCount={SetCount} SerializeMs={SerializeMs} SetTransportMs={SetTransportMs}")]
    private static partial void LogRedisPerf(
        ILogger logger,
        string operation,
        string correlationId,
        int getCount,
        long getTransportMs,
        long deserializeMs,
        long maxSingleGetTransportMs,
        int setCount,
        long serializeMs,
        long setTransportMs);

    [LoggerMessage(
        EventId = 7302,
        Level = LogLevel.Information,
        Message = "RedisMuxPerf Operation={Operation} CorrelationId={CorrelationId} IsConnected={IsConnected} IsConnecting={IsConnecting} TotalOutstanding={TotalOutstanding} InteractiveAwaitingResponse={InteractiveAwaitingResponse} InteractiveCompletedAsync={InteractiveCompletedAsync} InteractiveFailedAsync={InteractiveFailedAsync}")]
    private static partial void LogRedisMuxPerf(
        ILogger logger,
        string operation,
        string correlationId,
        bool isConnected,
        bool isConnecting,
        int totalOutstanding,
        int interactiveAwaitingResponse,
        long interactiveCompletedAsync,
        long interactiveFailedAsync);

    [LoggerMessage(
        EventId = 7303,
        Level = LogLevel.Information,
        Message = "RuntimePerf Operation={Operation} CorrelationId={CorrelationId} ThreadPoolThreadCount={ThreadPoolThreadCount} WorkerAvailable={WorkerAvailable} WorkerMax={WorkerMax} PendingWorkItemCount={PendingWorkItemCount} PendingWorkItemDelta={PendingWorkItemDelta} GcHeapMb={GcHeapMb} Gen2Collections={Gen2Collections} WorkingSetMb={WorkingSetMb}")]
    private static partial void LogRuntimePerf(
        ILogger logger,
        string operation,
        string correlationId,
        int threadPoolThreadCount,
        int workerAvailable,
        int workerMax,
        long pendingWorkItemCount,
        long gcHeapMb,
        int gen2Collections,
        long workingSetMb,
        long pendingWorkItemDelta);
}
