using Microsoft.AspNetCore.Http;
using MovieApp.Api.Errors;
using Serilog.Events;

namespace MovieApp.Api.Observability;

internal static class RequestLoggingLevelPolicy
{
    public static LogEventLevel GetLevel(HttpContext httpContext, Exception? exception)
    {
        if (exception is not null
            && RequestAbortExceptionHandling.IsRequestAbortedCancellation(httpContext, exception))
        {
            return LogEventLevel.Debug;
        }

        if (exception is not null)
        {
            return LogEventLevel.Error;
        }

        if (IsSuccessfulRoutineLiveHealthProbe(httpContext))
        {
            return LogEventLevel.Debug;
        }

        return httpContext.Response.StatusCode > 499
            ? LogEventLevel.Error
            : LogEventLevel.Information;
    }

    internal static bool IsSuccessfulRoutineLiveHealthProbe(HttpContext httpContext) =>
        httpContext.Response.StatusCode < 400
        && string.Equals(httpContext.Request.Path.Value, "/health/live", StringComparison.OrdinalIgnoreCase);
}
