using System.Text.Json;
using Microsoft.Extensions.Caching.Distributed;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using MovieApp.Infrastructure.Caching;
using MovieApp.Infrastructure.Configuration;
using StackExchange.Redis;

namespace MovieApp.UnitTests.Caching;

public sealed class RedisCacheServiceTests
{
    [Fact]
    public async Task GetAsyncReturnsNullWhenRedisInfrastructureFails()
    {
        var cache = CreateService(
            new ThrowingDistributedCache(new RedisConnectionException(ConnectionFailureType.UnableToConnect, "No connection available")),
            useRedisBackend: true);

        var result = await cache.GetAsync<TestCacheEntry>("movie-search:test");

        Assert.Null(result);
    }

    [Fact]
    public async Task SetAsyncDoesNotThrowWhenRedisInfrastructureFails()
    {
        var cache = CreateService(
            new ThrowingDistributedCache(new TimeoutException("The operation timed out.")),
            useRedisBackend: true);

        var exception = await Record.ExceptionAsync(() =>
            cache.SetAsync("movie-search:test", new TestCacheEntry("value"), TimeSpan.FromMinutes(5)));

        Assert.Null(exception);
    }

    [Fact]
    public async Task RemoveAsyncDoesNotThrowWhenRedisInfrastructureFails()
    {
        var cache = CreateService(
            new ThrowingDistributedCache(new TimeoutException("Timeout performing read")),
            useRedisBackend: true);

        var exception = await Record.ExceptionAsync(() => cache.RemoveAsync("movie-search:test"));

        Assert.Null(exception);
    }

    [Fact]
    public async Task GetAsyncReturnsCachedValueWhenRedisIsHealthy()
    {
        var distributedCache = new MemoryDistributedCache(Options.Create(new MemoryDistributedCacheOptions()));
        var cache = CreateService(distributedCache, useRedisBackend: true);
        var entry = new TestCacheEntry("cached-value");

        await cache.SetAsync("movie-search:test", entry, TimeSpan.FromMinutes(5));
        var result = await cache.GetAsync<TestCacheEntry>("movie-search:test");

        Assert.NotNull(result);
        Assert.Equal("cached-value", result.Value);
    }

    [Fact]
    public async Task GetAsyncPropagatesNonInfrastructureExceptions()
    {
        var cache = CreateService(
            new ThrowingDistributedCache(new InvalidOperationException("Unexpected application failure")),
            useRedisBackend: true);

        await Assert.ThrowsAsync<InvalidOperationException>(() =>
            cache.GetAsync<TestCacheEntry>("movie-search:test"));
    }

    [Fact]
    public async Task GetAsyncPropagatesJsonErrorsWithoutTreatingThemAsCacheMiss()
    {
        var distributedCache = new MemoryDistributedCache(Options.Create(new MemoryDistributedCacheOptions()));
        await distributedCache.SetStringAsync(
            "MovieApp:movie-search:test",
            "{ invalid json",
            new DistributedCacheEntryOptions { AbsoluteExpirationRelativeToNow = TimeSpan.FromMinutes(1) });

        var cache = CreateService(distributedCache, useRedisBackend: true);

        await Assert.ThrowsAsync<JsonException>(() =>
            cache.GetAsync<TestCacheEntry>("movie-search:test"));
    }

    [Fact]
    public async Task InMemoryBackendDoesNotSuppressUnexpectedExceptions()
    {
        var cache = CreateService(
            new ThrowingDistributedCache(new InvalidOperationException("Unexpected application failure")),
            useRedisBackend: false);

        await Assert.ThrowsAsync<InvalidOperationException>(() =>
            cache.GetAsync<TestCacheEntry>("movie-search:test"));
    }

    [Fact]
    public async Task FailureLoggerSuppressesRepeatedLogsWithinInterval()
    {
        var logger = new CollectingLogger<RedisCacheService>();
        var failureLogger = new RedisCacheFailureLogger();
        var cache = new RedisCacheService(
            new ThrowingDistributedCache(new TimeoutException("timeout")),
            Options.Create(new RedisOptions { ConnectionString = "localhost:6379" }),
            logger,
            failureLogger);

        await cache.GetAsync<TestCacheEntry>("one");
        await cache.GetAsync<TestCacheEntry>("two");
        await cache.GetAsync<TestCacheEntry>("three");

        Assert.Contains(logger.Messages, message => message.Contains("Redis cache is unavailable", StringComparison.Ordinal));
        Assert.Single(logger.Messages, message => message.Contains("Redis cache get failed", StringComparison.Ordinal));
        Assert.Equal(2, logger.Messages.Count);
        Assert.DoesNotContain(logger.Messages, message => message.Contains("localhost:6379", StringComparison.Ordinal));
        Assert.DoesNotContain(logger.Messages, message => message.Contains("password", StringComparison.OrdinalIgnoreCase));
    }

    private static RedisCacheService CreateService(
        IDistributedCache distributedCache,
        bool useRedisBackend)
    {
        var options = new RedisOptions
        {
            ConnectionString = useRedisBackend ? "localhost:6379" : string.Empty,
            InstanceName = "MovieApp:"
        };

        return new RedisCacheService(
            distributedCache,
            Options.Create(options),
            NullLogger<RedisCacheService>.Instance,
            new RedisCacheFailureLogger());
    }

    private sealed record TestCacheEntry(string Value);

    private sealed class ThrowingDistributedCache(Exception exception) : IDistributedCache
    {
        public byte[]? Get(string key) => throw exception;

        public Task<byte[]?> GetAsync(string key, CancellationToken token = default) =>
            throw exception;

        public void Refresh(string key) => throw exception;

        public Task RefreshAsync(string key, CancellationToken token = default) =>
            throw exception;

        public void Remove(string key) => throw exception;

        public Task RemoveAsync(string key, CancellationToken token = default) =>
            throw exception;

        public void Set(string key, byte[] value, DistributedCacheEntryOptions options) =>
            throw exception;

        public Task SetAsync(
            string key,
            byte[] value,
            DistributedCacheEntryOptions options,
            CancellationToken token = default) =>
            throw exception;
    }

    private sealed class CollectingLogger<T> : ILogger<T>
    {
        public List<string> Messages { get; } = [];

        public IDisposable BeginScope<TState>(TState state)
            where TState : notnull => NullScope.Instance;

        public bool IsEnabled(LogLevel logLevel) => true;

        public void Log<TState>(
            LogLevel logLevel,
            EventId eventId,
            TState state,
            Exception? exception,
            Func<TState, Exception?, string> formatter)
        {
            Messages.Add(formatter(state, exception));
        }

        private sealed class NullScope : IDisposable
        {
            public static NullScope Instance { get; } = new();

            public void Dispose()
            {
            }
        }
    }
}
