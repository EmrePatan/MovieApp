using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using MovieApp.Application.Abstractions.Caching;
using MovieApp.Infrastructure.Caching;
using MovieApp.Infrastructure.Configuration;
using MovieApp.UnitTests.Search;
namespace MovieApp.UnitTests.Caching;

public sealed class SearchRefreshLockRenewalTests
{
    [Fact]
    public async Task TryRenewAsyncReturnsTrueForMatchingLocalOwnershipToken()
    {
        var lockService = CreateLocalOnlyLockService();
        var handle = await lockService.TryAcquireAsync("search-refresh-lock:batman:All:1", TimeSpan.FromSeconds(30));

        Assert.NotNull(handle);
        Assert.True(await lockService.TryRenewAsync(
            handle!.LockKey,
            handle.LockToken,
            handle.Backend,
            TimeSpan.FromSeconds(30)));
    }

    [Fact]
    public async Task TryRenewAsyncReturnsFalseWhenLocalOwnershipTokenDoesNotMatch()
    {
        var lockService = CreateLocalOnlyLockService();
        var handle = await lockService.TryAcquireAsync("search-refresh-lock:batman:All:1", TimeSpan.FromSeconds(30));

        Assert.NotNull(handle);
        Assert.False(await lockService.TryRenewAsync(
            handle!.LockKey,
            "wrong-token",
            handle.Backend,
            TimeSpan.FromSeconds(30)));
    }

    [Fact]
    public async Task ReleaseDoesNotClearLockOwnedByDifferentTokenAfterFailedRenewal()
    {
        var gate = new LocalSearchRefreshSingleFlightGate();
        var lockService = new SearchRefreshLockService(
            new ServiceCollection().BuildServiceProvider(),
            Options.Create(new RedisOptions { ConnectionString = string.Empty }),
            gate,
            new SearchRefreshLockDiagnostics(),
            NullLogger<SearchRefreshLockService>.Instance);

        var first = await lockService.TryAcquireAsync("search-refresh-lock:friends:All:1", TimeSpan.FromSeconds(30));
        Assert.NotNull(first);

        await lockService.ReleaseAsync(first!.LockKey, "wrong-token", first.Backend);
        var second = await lockService.TryAcquireAsync("search-refresh-lock:friends:All:1", TimeSpan.FromSeconds(30));
        Assert.Null(second);

        await lockService.ReleaseAsync(first.LockKey, first.LockToken, first.Backend);
        var third = await lockService.TryAcquireAsync("search-refresh-lock:friends:All:1", TimeSpan.FromSeconds(30));
        Assert.NotNull(third);
    }

    private static SearchRefreshLockService CreateLocalOnlyLockService() =>
        new(
            new ServiceCollection().BuildServiceProvider(),
            Options.Create(new RedisOptions { ConnectionString = string.Empty }),
            new LocalSearchRefreshSingleFlightGate(),
            new SearchRefreshLockDiagnostics(),
            NullLogger<SearchRefreshLockService>.Instance);
}
