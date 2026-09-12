using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using MovieApp.Application.Abstractions.Caching;
using MovieApp.Infrastructure.Caching;
using MovieApp.Infrastructure.Configuration;

namespace MovieApp.UnitTests.Caching;

public sealed class SearchRefreshCompletionSignalTests
{
    [Fact]
    public async Task PublishAndReadOutcomeUsesLocalFallbackWhenRedisUnavailable()
    {
        var registry = new LocalSearchRefreshCompletionRegistry();
        var signal = new SearchRefreshCompletionSignal(
            connectionMultiplexer: null,
            Options.Create(new RedisOptions { ConnectionString = string.Empty }),
            registry,
            NullLogger<SearchRefreshCompletionSignal>.Instance);

        const string lockKey = "search-refresh-lock:friends:All:1";

        await signal.PublishAsync(
            lockKey,
            SearchRefreshAttemptOutcome.Succeeded,
            TimeSpan.FromSeconds(30));

        var outcome = await signal.TryGetOutcomeAsync(lockKey);

        Assert.Equal(SearchRefreshAttemptOutcome.Succeeded, outcome);
    }

    [Fact]
    public async Task FailedOutcomeIsReadableByWaiters()
    {
        var signal = CreateLocalSignal();
        const string lockKey = "search-refresh-lock:batman:All:1";

        await signal.PublishAsync(
            lockKey,
            SearchRefreshAttemptOutcome.Failed,
            TimeSpan.FromSeconds(30));

        var outcome = await signal.TryGetOutcomeAsync(lockKey);

        Assert.Equal(SearchRefreshAttemptOutcome.Failed, outcome);
    }

    private static SearchRefreshCompletionSignal CreateLocalSignal() =>
        new(
            connectionMultiplexer: null,
            Options.Create(new RedisOptions { ConnectionString = string.Empty }),
            new LocalSearchRefreshCompletionRegistry(),
            NullLogger<SearchRefreshCompletionSignal>.Instance);
}
