using Microsoft.Extensions.Logging;

namespace MovieApp.Api.Errors;

internal static partial class GlobalExceptionHandlerLogMessages
{
    [LoggerMessage(
        EventId = 5001,
        Level = LogLevel.Error,
        Message = "Unhandled exception mapped to {StatusCode}. TraceId={TraceId}")]
    internal static partial void LogMappedException(
        ILogger logger,
        Exception exception,
        int statusCode,
        string traceId);

    [LoggerMessage(
        EventId = 5002,
        Level = LogLevel.Error,
        Message = "Unhandled exception. TraceId={TraceId}")]
    internal static partial void LogUnhandledException(
        ILogger logger,
        Exception exception,
        string traceId);
}
