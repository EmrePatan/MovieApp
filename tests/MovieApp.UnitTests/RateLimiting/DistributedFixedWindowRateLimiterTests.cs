using System.Threading.RateLimiting;
using MovieApp.Application.Abstractions.RateLimiting;
using MovieApp.Api.RateLimiting;

namespace MovieApp.UnitTests.RateLimiting;

public sealed class DistributedFixedWindowRateLimiterTests
{
    [Fact]
    public async Task AcquireAsyncReturnsRejectedLeaseWithRetryAfterWhenLimitExceeded()
    {
        var store = new SequenceRateLimitCounterStore([
            new RateLimitCounterResult(true, null),
            new RateLimitCounterResult(false, TimeSpan.FromSeconds(42))
        ]);

        var limiter = new DistributedFixedWindowRateLimiter(store, "search:test", 1, TimeSpan.FromMinutes(1));

        Assert.True((await limiter.AcquireAsync(cancellationToken: CancellationToken.None)).IsAcquired);

        var rejectedLease = await limiter.AcquireAsync(cancellationToken: CancellationToken.None);
        Assert.False(rejectedLease.IsAcquired);
        Assert.True(rejectedLease.TryGetMetadata(MetadataName.RetryAfter.Name, out var retryAfter));
        Assert.Equal(TimeSpan.FromSeconds(42), retryAfter);
    }

    private sealed class SequenceRateLimitCounterStore(IReadOnlyList<RateLimitCounterResult> results) : IRateLimitCounterStore
    {
        private int _index;

        public Task<RateLimitCounterResult> TryAcquireAsync(
            string partitionKey,
            int permitLimit,
            TimeSpan window,
            CancellationToken cancellationToken = default)
        {
            var result = results[Math.Min(_index, results.Count - 1)];
            _index++;
            return Task.FromResult(result);
        }
    }
}
