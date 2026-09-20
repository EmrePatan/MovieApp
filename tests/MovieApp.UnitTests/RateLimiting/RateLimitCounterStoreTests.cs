using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.FileProviders;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using MovieApp.Application.Abstractions.RateLimiting;
using MovieApp.Infrastructure.Configuration;
using MovieApp.Infrastructure.RateLimiting;

namespace MovieApp.UnitTests.RateLimiting;

public sealed class InMemoryRateLimitCounterStoreTests
{
    [Fact]
    public async Task TryAcquireAsyncEnforcesFixedWindowPermitLimit()
    {
        var store = new InMemoryRateLimitCounterStore();
        var window = TimeSpan.FromMinutes(1);

        Assert.True((await store.TryAcquireAsync("client-a", 2, window)).IsAcquired);
        Assert.True((await store.TryAcquireAsync("client-a", 2, window)).IsAcquired);

        var rejected = await store.TryAcquireAsync("client-a", 2, window);
        Assert.False(rejected.IsAcquired);
        Assert.NotNull(rejected.RetryAfter);
    }
}

public sealed class CompositeRateLimitCounterStoreTests
{
    [Fact]
    public async Task TryAcquireAsyncFallsBackToInMemoryWhenRedisStoreFails()
    {
        var composite = new CompositeRateLimitCounterStore(
            new RedisRateLimitCounterStore(
                new ServiceCollection().BuildServiceProvider(),
                Options.Create(new RedisOptions { ConnectionString = string.Empty }),
                NullLogger<RedisRateLimitCounterStore>.Instance),
            new InMemoryRateLimitCounterStore(),
            new FakeHostEnvironment("Development"),
            NullLogger<CompositeRateLimitCounterStore>.Instance);

        var result = await composite.TryAcquireAsync("client-a", 5, TimeSpan.FromMinutes(1));
        Assert.True(result.IsAcquired);
    }

    [Fact]
    public async Task TryAcquireAsyncDeniesRequestsInProductionWhenRedisStoreFails()
    {
        var composite = new CompositeRateLimitCounterStore(
            new RedisRateLimitCounterStore(
                new ServiceCollection().BuildServiceProvider(),
                Options.Create(new RedisOptions { ConnectionString = string.Empty }),
                NullLogger<RedisRateLimitCounterStore>.Instance),
            new InMemoryRateLimitCounterStore(),
            new FakeHostEnvironment("Production"),
            NullLogger<CompositeRateLimitCounterStore>.Instance);

        var result = await composite.TryAcquireAsync("client-a", 5, TimeSpan.FromMinutes(1));

        Assert.False(result.IsAcquired);
        Assert.NotNull(result.RetryAfter);
    }

    private sealed class FakeHostEnvironment(string environmentName) : IHostEnvironment
    {
        public string EnvironmentName { get; set; } = environmentName;

        public string ApplicationName { get; set; } = "MovieApp.UnitTests";

        public string ContentRootPath { get; set; } = AppContext.BaseDirectory;

        public IFileProvider ContentRootFileProvider { get; set; } = new NullFileProvider();
    }
}
