namespace MovieApp.Application.Services.Search;

internal static class SearchRequestCancellation
{
    internal static bool IsCallerCancellation(Exception exception, CancellationToken cancellationToken) =>
        exception is OperationCanceledException && cancellationToken.IsCancellationRequested;
}
