namespace MovieApp.Application.Exceptions;

public static class ProviderFailureFilter
{
    /// <summary>
    /// True for genuine upstream failures. False when the caller's own token was cancelled
    /// (client disconnected / request aborted), which must propagate as cancellation rather
    /// than be reported as a provider outage (503) and logged as a TMDB failure.
    /// Provider timeouts are unaffected: they surface as transient provider exceptions or as
    /// cancellations of a token the caller did not cancel.
    /// </summary>
    public static bool IsProviderFailure(Exception exception, CancellationToken callerToken) =>
        !(exception is OperationCanceledException && callerToken.IsCancellationRequested);
}
