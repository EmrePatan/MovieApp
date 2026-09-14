using Microsoft.AspNetCore.Http;
using MovieApp.Api.Errors;

namespace MovieApp.UnitTests.Errors;

public sealed class RequestAbortExceptionHandlingTests
{
    [Fact]
    public void ReturnsTrueWhenOperationCanceledExceptionMatchesRequestAborted()
    {
        using var cancellation = new CancellationTokenSource();
        cancellation.Cancel();

        var httpContext = new DefaultHttpContext
        {
            RequestAborted = cancellation.Token,
        };

        Assert.True(RequestAbortExceptionHandling.IsRequestAbortedCancellation(
            httpContext,
            new OperationCanceledException(cancellation.Token)));
    }

    [Fact]
    public void ReturnsTrueForTaskCanceledExceptionWhenRequestAborted()
    {
        using var cancellation = new CancellationTokenSource();
        cancellation.Cancel();

        var httpContext = new DefaultHttpContext
        {
            RequestAborted = cancellation.Token,
        };

        Assert.True(RequestAbortExceptionHandling.IsRequestAbortedCancellation(
            httpContext,
            new TaskCanceledException()));
    }

    [Fact]
    public void ReturnsFalseWhenRequestIsNotAborted()
    {
        var httpContext = new DefaultHttpContext();

        Assert.False(RequestAbortExceptionHandling.IsRequestAbortedCancellation(
            httpContext,
            new OperationCanceledException()));
    }

    [Fact]
    public void ReturnsFalseForNonCancellationExceptions()
    {
        using var cancellation = new CancellationTokenSource();
        cancellation.Cancel();

        var httpContext = new DefaultHttpContext
        {
            RequestAborted = cancellation.Token,
        };

        Assert.False(RequestAbortExceptionHandling.IsRequestAbortedCancellation(
            httpContext,
            new InvalidOperationException()));
    }
}
