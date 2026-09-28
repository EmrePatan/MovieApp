using Microsoft.Extensions.Caching.Memory;
using MovieApp.Infrastructure.Identity;

namespace MovieApp.UnitTests.Authentication;

public sealed class SecurityStampCacheTests
{
    [Fact]
    public void TtlIsFiveSeconds()
    {
        Assert.Equal(TimeSpan.FromSeconds(5), SecurityStampCache.Ttl);
    }

    [Fact]
    public async Task GetOrLoadAsync_DoesNotRepopulateStampInvalidatedDuringLoad()
    {
        using var cache = new MemoryCache(new MemoryCacheOptions());
        var stampCache = new SecurityStampCache();
        var userId = Guid.NewGuid();
        var started = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var release = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var oldStamp = Guid.NewGuid();
        var newStamp = Guid.NewGuid();

        var load = stampCache.GetOrLoadAsync(
            cache,
            userId,
            async _ =>
            {
                started.TrySetResult();
                await release.Task;
                return oldStamp;
            },
            CancellationToken.None);

        await started.Task;
        SecurityStampCache.Invalidate(cache, userId);
        release.TrySetResult();

        Assert.Equal(oldStamp, await load);

        var reloaded = await stampCache.GetOrLoadAsync(
            cache,
            userId,
            _ => Task.FromResult<Guid?>(newStamp),
            CancellationToken.None);

        Assert.Equal(newStamp, reloaded);
    }

    [Fact]
    public async Task GetOrLoadAsync_NewGenerationDoesNotJoinPreInvalidationInflightLoad()
    {
        using var cache = new MemoryCache(new MemoryCacheOptions());
        var stampCache = new SecurityStampCache();
        var userId = Guid.NewGuid();
        var started = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var release = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var staleStamp = Guid.NewGuid();
        var freshStamp = Guid.NewGuid();
        var staleLoadCount = 0;
        var freshLoadCount = 0;

        var staleLoad = stampCache.GetOrLoadAsync(
            cache,
            userId,
            async _ =>
            {
                staleLoadCount++;
                started.TrySetResult();
                await release.Task;
                return staleStamp;
            },
            CancellationToken.None);

        await started.Task;
        SecurityStampCache.Invalidate(cache, userId);

        var freshLoad = stampCache.GetOrLoadAsync(
            cache,
            userId,
            _ =>
            {
                freshLoadCount++;
                return Task.FromResult<Guid?>(freshStamp);
            },
            CancellationToken.None);

        Assert.Equal(freshStamp, await freshLoad);
        Assert.Equal(1, freshLoadCount);

        release.TrySetResult();
        Assert.Equal(staleStamp, await staleLoad);
        Assert.Equal(1, staleLoadCount);
        Assert.True(cache.TryGetValue(SecurityStampCache.Key(userId), out SecurityStampCache.SecurityStampCacheEntry? cached));
        Assert.Equal(freshStamp, cached!.Stamp);
    }
}
