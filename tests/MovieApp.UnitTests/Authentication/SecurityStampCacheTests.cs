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
}
