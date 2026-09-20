using System.Net;
using System.Threading.RateLimiting;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.FileProviders;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using MovieApp.Api.RateLimiting;
using MovieApp.Application.Abstractions.RateLimiting;
using MovieApp.Infrastructure.Configuration;
using MovieApp.Infrastructure.RateLimiting;

namespace MovieApp.UnitTests.RateLimiting;

public sealed class AuthDistributedRateLimitTests
{
    [Fact]
    public async Task AuthLimitersShareCounterStoreAcrossInstances()
    {
        var store = new InMemoryRateLimitCounterStore();
        const string storePartitionKey = $"{AuthRateLimitPolicies.Login}:10.0.0.1:/api/auth/login";
        var window = TimeSpan.FromMinutes(1);

        var firstLimiter = new DistributedFixedWindowRateLimiter(store, storePartitionKey, 2, window);
        var secondLimiter = new DistributedFixedWindowRateLimiter(store, storePartitionKey, 2, window);

        Assert.True((await firstLimiter.AcquireAsync(cancellationToken: CancellationToken.None)).IsAcquired);
        Assert.True((await secondLimiter.AcquireAsync(cancellationToken: CancellationToken.None)).IsAcquired);

        var rejectedLease = await firstLimiter.AcquireAsync(cancellationToken: CancellationToken.None);
        Assert.False(rejectedLease.IsAcquired);
        Assert.True(rejectedLease.TryGetMetadata(MetadataName.RetryAfter.Name, out _));
    }

    [Fact]
    public void AuthPartitionKeyUsesClientIpAndEndpoint()
    {
        var partitionKeyFactory = DistributedRateLimitPolicyFactory.CreateClientIpEndpointPartitionKeyFactory();
        var httpContext = CreateHttpContext(new IPAddress(new byte[] { 203, 0, 113, 9 }), "/api/auth/login");

        var partitionKey = partitionKeyFactory(httpContext);

        Assert.Equal("203.0.113.9:/api/auth/login", partitionKey);
    }

    [Fact]
    public async Task AuthDistributedLimiterFailsClosedInProductionWhenRedisIsUnavailable()
    {
        var composite = new CompositeRateLimitCounterStore(
            new RedisRateLimitCounterStore(
                new ServiceCollection().BuildServiceProvider(),
                Options.Create(new RedisOptions { ConnectionString = string.Empty }),
                NullLogger<RedisRateLimitCounterStore>.Instance),
            new InMemoryRateLimitCounterStore(),
            new FakeHostEnvironment("Production"),
            NullLogger<CompositeRateLimitCounterStore>.Instance);

        var limiter = new DistributedFixedWindowRateLimiter(
            composite,
            $"{AuthRateLimitPolicies.Login}:203.0.113.9:/api/auth/login",
            5,
            TimeSpan.FromMinutes(1));

        var lease = await limiter.AcquireAsync(cancellationToken: CancellationToken.None);

        Assert.False(lease.IsAcquired);
        Assert.True(lease.TryGetMetadata(MetadataName.RetryAfter.Name, out var retryAfter));
        Assert.Equal(TimeSpan.FromSeconds(30), retryAfter);
    }

    private static DefaultHttpContext CreateHttpContext(IPAddress remoteIp, string path)
    {
        var httpContext = new DefaultHttpContext
        {
            RequestServices = new ServiceCollection().BuildServiceProvider()
        };
        httpContext.Connection.RemoteIpAddress = remoteIp;
        httpContext.Request.Path = path;
        return httpContext;
    }

    private sealed class FakeHostEnvironment(string environmentName) : IHostEnvironment
    {
        public string EnvironmentName { get; set; } = environmentName;

        public string ApplicationName { get; set; } = "MovieApp.UnitTests";

        public string ContentRootPath { get; set; } = AppContext.BaseDirectory;

        public IFileProvider ContentRootFileProvider { get; set; } = new NullFileProvider();
    }
}
