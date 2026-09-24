using MovieApp.Application.Exceptions;

namespace MovieApp.UnitTests.Search;

public sealed class ProviderFailureFilterTests
{
    [Fact]
    public void CallerCancellationIsNotAProviderFailure()
    {
        using var cancellation = new CancellationTokenSource();
        cancellation.Cancel();

        Assert.False(ProviderFailureFilter.IsProviderFailure(
            new OperationCanceledException(cancellation.Token),
            cancellation.Token));
    }

    [Fact]
    public void CancellationOfAnotherTokenIsAProviderFailure() =>
        Assert.True(ProviderFailureFilter.IsProviderFailure(
            new TaskCanceledException("upstream timeout"),
            CancellationToken.None));

    [Fact]
    public void UpstreamErrorsAreProviderFailuresEvenAfterCallerCancelled()
    {
        using var cancellation = new CancellationTokenSource();
        cancellation.Cancel();

        Assert.True(ProviderFailureFilter.IsProviderFailure(
            new HttpRequestException("connection reset"),
            cancellation.Token));
    }
}
