using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using MovieApp.Application.Abstractions.Caching;
using MovieApp.Infrastructure.Caching;
using MovieApp.Infrastructure.Configuration;
using MovieApp.UnitTests.Search;

namespace MovieApp.UnitTests.Caching;

public sealed class SearchRefreshLockServiceTests
{
    [Fact]
    public async Task LocalSingleFlightAllowsOnlyOneConcurrentOwnerPerKey()
    {
        var lockService = CreateLocalOnlyLockService();

        var first = await lockService.TryAcquireAsync("search-refresh-lock:batman:All:1", TimeSpan.FromSeconds(30));
        var second = await lockService.TryAcquireAsync("search-refresh-lock:batman:All:1", TimeSpan.FromSeconds(30));

        Assert.NotNull(first);
        Assert.Null(second);
        Assert.Equal(SearchRefreshLockBackend.LocalSingleFlight, first!.Backend);

        await lockService.ReleaseAsync(first.LockKey, first.LockToken, first.Backend);

        var third = await lockService.TryAcquireAsync("search-refresh-lock:batman:All:1", TimeSpan.FromSeconds(30));
        Assert.NotNull(third);
    }

    [Fact]
    public async Task LocalSingleFlightDoesNotSerializeDifferentQueries()
    {
        var lockService = CreateLocalOnlyLockService();

        var batman = await lockService.TryAcquireAsync("search-refresh-lock:batman:All:1", TimeSpan.FromSeconds(30));
        var matrix = await lockService.TryAcquireAsync("search-refresh-lock:matrix:All:1", TimeSpan.FromSeconds(30));

        Assert.NotNull(batman);
        Assert.NotNull(matrix);
    }

    [Fact]
    public void ReleaseOnlyDeletesMatchingOwnershipToken()
    {
        var gate = new LocalSearchRefreshSingleFlightGate();

        Assert.True(gate.TryAcquire("search-refresh-lock:friends:All:1", out var token));
        Assert.False(gate.Release("search-refresh-lock:friends:All:1", "wrong-token"));
        Assert.False(gate.TryAcquire("search-refresh-lock:friends:All:1", out _));

        Assert.True(gate.Release("search-refresh-lock:friends:All:1", token));
        Assert.True(gate.TryAcquire("search-refresh-lock:friends:All:1", out _));
    }

    private static SearchRefreshLockService CreateLocalOnlyLockService() =>
        new(
            new ServiceCollection().BuildServiceProvider(),
            Options.Create(new RedisOptions { ConnectionString = string.Empty }),
            new LocalSearchRefreshSingleFlightGate(),
            new SearchRefreshLockDiagnostics(),
            NullLogger<SearchRefreshLockService>.Instance);
}

public sealed class InMemorySearchRefreshLockServiceTests
{
    [Fact]
    public async Task ReleaseOnlyDeletesMatchingOwnershipToken()
    {
        var lockService = new SearchTestDoubles.InMemorySearchRefreshLockService();
        var handle = await lockService.TryAcquireAsync("search-refresh-lock:friends:All:1", TimeSpan.FromSeconds(30));

        Assert.NotNull(handle);
        await lockService.ReleaseAsync(handle!.LockKey, "wrong-token", handle.Backend);
        await lockService.ReleaseAsync(handle.LockKey, handle.LockToken, handle.Backend);

        var second = await lockService.TryAcquireAsync("search-refresh-lock:friends:All:1", TimeSpan.FromSeconds(30));
        Assert.NotNull(second);
    }
}
