using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using MovieApp.Infrastructure.Caching;
using MovieApp.Infrastructure.Configuration;
using MovieApp.UnitTests.Search;

namespace MovieApp.UnitTests.Caching;

public sealed class RedisSearchRefreshLockServiceTests
{
    [Fact]
    public async Task TryAcquireFailsOpenWhenRedisIsNotConfigured()
    {
        var service = new RedisSearchRefreshLockService(
            connectionMultiplexer: null,
            Options.Create(new RedisOptions { ConnectionString = string.Empty }),
            NullLogger<RedisSearchRefreshLockService>.Instance);

        var handle = await service.TryAcquireAsync("search-refresh-lock:friends:All:1", TimeSpan.FromSeconds(30));

        Assert.NotNull(handle);
        Assert.False(string.IsNullOrWhiteSpace(handle!.LockToken));
    }

    [Fact]
    public async Task ReleaseDoesNotThrowWhenRedisIsNotConfigured()
    {
        var service = new RedisSearchRefreshLockService(
            connectionMultiplexer: null,
            Options.Create(new RedisOptions { ConnectionString = string.Empty }),
            NullLogger<RedisSearchRefreshLockService>.Instance);

        await service.ReleaseAsync("search-refresh-lock:friends:All:1", Guid.NewGuid().ToString("N"));
    }
}

public sealed class InMemorySearchRefreshLockServiceTests
{
    [Fact]
    public async Task ReleaseOnlyDeletesMatchingOwnershipToken()
    {
        var lockService = new SearchTestDoubles.InMemorySearchRefreshLockService();
        var handle = await lockService.TryAcquireAsync("search-refresh-lock:friends:All:1", TimeSpan.FromSeconds(30));

        Assert.NotNull(handle);
        await lockService.ReleaseAsync(handle!.LockKey, "wrong-token");
        Assert.True(lockService.IsLocked(handle.LockKey));

        await lockService.ReleaseAsync(handle.LockKey, handle.LockToken);
        Assert.False(lockService.IsLocked(handle.LockKey));
    }

    [Fact]
    public async Task ExpiredLockCanBeAcquiredByAnotherRequest()
    {
        var lockService = new SearchTestDoubles.InMemorySearchRefreshLockService();
        var first = await lockService.TryAcquireAsync("search-refresh-lock:batman:All:1", TimeSpan.FromSeconds(30));
        Assert.NotNull(first);

        lockService.ExpireLock(first!.LockKey);

        var second = await lockService.TryAcquireAsync("search-refresh-lock:batman:All:1", TimeSpan.FromSeconds(30));
        Assert.NotNull(second);
        Assert.NotEqual(first.LockToken, second!.LockToken);
    }
}
