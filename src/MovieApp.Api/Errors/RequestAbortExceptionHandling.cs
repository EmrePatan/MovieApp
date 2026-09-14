namespace MovieApp.Api.Errors;

internal static class RequestAbortExceptionHandling
{
    internal static bool IsRequestAbortedCancellation(HttpContext httpContext, Exception exception) =>
        exception is OperationCanceledException
        && httpContext.RequestAborted.IsCancellationRequested;
}
