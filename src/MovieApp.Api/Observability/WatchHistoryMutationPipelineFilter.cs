using System.Diagnostics;
using Microsoft.AspNetCore.Mvc.Filters;
using Microsoft.Extensions.Logging;

namespace MovieApp.Api.Observability;

internal sealed partial class WatchHistoryMutationPipelineFilter(ILogger<WatchHistoryMutationPipelineFilter> logger)
    : IAsyncActionFilter, IAsyncResultFilter
{
    private const string ActionStartTicksKey = "WatchHistory.ActionStartTicks";

    public async Task OnActionExecutionAsync(ActionExecutingContext context, ActionExecutionDelegate next)
    {
        if (!IsWatchHistoryMutation(context.HttpContext.Request))
        {
            await next();
            return;
        }

        var actionStart = Stopwatch.GetTimestamp();
        context.HttpContext.Items[ActionStartTicksKey] = actionStart;

        if (context.HttpContext.Items.TryGetValue(WatchHistoryMutationPerfContext.PipelineStartTicksKey, out var pipelineStartObj) &&
            pipelineStartObj is long pipelineStart)
        {
            context.HttpContext.Items[WatchHistoryMutationPerfContext.PreServiceMsKey] =
                ElapsedMilliseconds(pipelineStart, actionStart);
        }

        await next();
    }

    public Task OnResultExecutionAsync(ResultExecutingContext context, ResultExecutionDelegate next) =>
        next();

    public Task OnResultExecutedAsync(ResultExecutedContext context)
    {
        if (!IsWatchHistoryMutation(context.HttpContext.Request))
        {
            return Task.CompletedTask;
        }

        if (!context.HttpContext.Items.TryGetValue(ActionStartTicksKey, out var actionStartObj) ||
            actionStartObj is not long actionStart)
        {
            return Task.CompletedTask;
        }

        var actionEnd = Stopwatch.GetTimestamp();
        var actionTotalMs = ElapsedMilliseconds(actionStart, actionEnd);
        var serviceMs = context.HttpContext.Items.TryGetValue(WatchHistoryMutationPerfContext.ServiceMsKey, out var serviceMsObj) &&
                        serviceMsObj is long measuredServiceMs
            ? measuredServiceMs
            : 0L;
        var preServiceMs = context.HttpContext.Items.TryGetValue(WatchHistoryMutationPerfContext.PreServiceMsKey, out var preServiceObj) &&
                           preServiceObj is long measuredPreServiceMs
            ? measuredPreServiceMs
            : 0L;
        var postServiceMs = Math.Max(0, actionTotalMs - serviceMs);

        var correlationId = CorrelationIdAccessor.Get(context.HttpContext) ?? context.HttpContext.TraceIdentifier;

        context.HttpContext.Items["WatchHistory.ActionTotalMs"] = actionTotalMs;

        LogMutationPipeline(
            logger,
            context.HttpContext.Request.Path.Value ?? string.Empty,
            correlationId,
            preServiceMs,
            serviceMs,
            postServiceMs,
            actionTotalMs);

        return Task.CompletedTask;
    }

    private static bool IsWatchHistoryMutation(HttpRequest request) =>
        request.Method.Equals(HttpMethods.Post, StringComparison.OrdinalIgnoreCase) &&
        request.Path.StartsWithSegments("/api/watch-history", StringComparison.OrdinalIgnoreCase) &&
        (request.Path.Value?.Contains("/watch-state", StringComparison.OrdinalIgnoreCase) == true ||
         request.Path.Value?.Contains("/movies/", StringComparison.OrdinalIgnoreCase) == true);

    private static long ElapsedMilliseconds(long startTimestamp, long endTimestamp) =>
        (endTimestamp - startTimestamp) * 1000 / Stopwatch.Frequency;

    [LoggerMessage(
        EventId = 7102,
        Level = LogLevel.Information,
        Message = "WatchHistoryPerf MutationPipeline Path={Path} CorrelationId={CorrelationId} PreServiceMs={PreServiceMs} ServiceMs={ServiceMs} PostServiceMs={PostServiceMs} ActionTotalMs={ActionTotalMs}")]
    private static partial void LogMutationPipeline(
        ILogger logger,
        string path,
        string correlationId,
        long preServiceMs,
        long serviceMs,
        long postServiceMs,
        long actionTotalMs);
}
