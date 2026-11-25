using System.Net.Sockets;

namespace MovieApp.Api.Errors;

internal static class RequestAbortExceptionHandling
{
    /// <summary>
    /// True when the client has gone away and the exception is only a symptom of that: a plain
    /// cancellation, or a transport/driver failure caused by it. Npgsql surfaces a request that is
    /// aborted mid-handshake or mid-read as an NpgsqlException / IOException / ObjectDisposedException
    /// rather than an OperationCanceledException, so the whole exception chain is inspected.
    /// </summary>
    internal static bool IsRequestAbortedCancellation(HttpContext httpContext, Exception exception) =>
        httpContext.RequestAborted.IsCancellationRequested && ContainsAbortSymptom(exception);

    private static bool ContainsAbortSymptom(Exception? exception)
    {
        while (exception is not null)
        {
            if (exception is OperationCanceledException
                or IOException
                or SocketException
                or ObjectDisposedException
                || string.Equals(exception.GetType().Name, "NpgsqlException", StringComparison.Ordinal))
            {
                return true;
            }

            if (exception is AggregateException aggregate)
            {
                return aggregate.InnerExceptions.Any(ContainsAbortSymptom);
            }

            exception = exception.InnerException;
        }

        return false;
    }
}
