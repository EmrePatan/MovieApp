using System.Net;
using MovieApp.Infrastructure.Configuration;
using StackExchange.Redis;

namespace MovieApp.UnitTests.Configuration;

public sealed class RedisConnectionOptionsFactoryTests
{
    [Fact]
    public void ParseConnectionStringNormalizesRedisUri()
    {
        var options = RedisConnectionOptionsFactory.ParseConnectionString("redis://red-example-host:6379");

        var endpoint = AssertEndpoint(options);
        Assert.Equal("red-example-host", endpoint.Host);
        Assert.Equal(6379, endpoint.Port);
        Assert.False(options.Ssl);
        Assert.False(options.AbortOnConnectFail);
    }

    [Fact]
    public void ParseConnectionStringNormalizesHostPortFormat()
    {
        var options = RedisConnectionOptionsFactory.ParseConnectionString("red-example-host:6379");

        var endpoint = AssertEndpoint(options);
        Assert.Equal("red-example-host", endpoint.Host);
        Assert.Equal(6379, endpoint.Port);
        Assert.False(options.Ssl);
        Assert.False(options.AbortOnConnectFail);
    }

    [Fact]
    public void ParseConnectionStringNormalizesRedisUriWithExistingOptions()
    {
        var options = RedisConnectionOptionsFactory.ParseConnectionString(
            "redis://red-example-host:6379,connectTimeout=7000,abortConnect=true");

        var endpoint = AssertEndpoint(options);
        Assert.Equal("red-example-host", endpoint.Host);
        Assert.Equal(6379, endpoint.Port);
        Assert.Equal(7000, options.ConnectTimeout);
        Assert.False(options.AbortOnConnectFail);
    }

    [Fact]
    public void ParseConnectionStringEnablesTlsForRedissUri()
    {
        var options = RedisConnectionOptionsFactory.ParseConnectionString("rediss://red-example-host:6380");

        var endpoint = AssertEndpoint(options);
        Assert.Equal("red-example-host", endpoint.Host);
        Assert.Equal(6380, endpoint.Port);
        Assert.True(options.Ssl);
        Assert.False(options.AbortOnConnectFail);
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData("http://red-example-host:6379")]
    [InlineData("redis://")]
    public void ParseConnectionStringThrowsForMalformedInput(string connectionString)
    {
        Assert.ThrowsAny<ArgumentException>(
            () => RedisConnectionOptionsFactory.ParseConnectionString(connectionString));
    }

    [Fact]
    public void CreateAppliesConfiguredTimeoutsAndAbortOnConnectFailFalse()
    {
        var options = RedisConnectionOptionsFactory.Create(new RedisOptions
        {
            ConnectionString = "redis://red-example-host:6379",
            ConnectTimeoutMs = 4500,
            SyncTimeoutMs = 2500,
        });

        var endpoint = AssertEndpoint(options);
        Assert.Equal("red-example-host", endpoint.Host);
        Assert.Equal(6379, endpoint.Port);
        Assert.Equal(4500, options.ConnectTimeout);
        Assert.Equal(2500, options.SyncTimeout);
        Assert.False(options.AbortOnConnectFail);
    }

    [Fact]
    public void CreateHealthCheckConnectionStringUsesNormalizedConfiguration()
    {
        var healthCheckConnectionString = RedisConnectionOptionsFactory.CreateHealthCheckConnectionString(
            new RedisOptions
            {
                ConnectionString = "redis://red-example-host:6379",
            });

        Assert.Contains("red-example-host:6379", healthCheckConnectionString, StringComparison.Ordinal);
        Assert.DoesNotContain("redis://", healthCheckConnectionString, StringComparison.Ordinal);
        Assert.Contains("abortConnect=False", healthCheckConnectionString, StringComparison.OrdinalIgnoreCase);
    }

    private static DnsEndPoint AssertEndpoint(ConfigurationOptions options)
    {
        var endpoint = Assert.Single(options.EndPoints);
        var dnsEndPoint = Assert.IsType<DnsEndPoint>(endpoint);
        Assert.DoesNotContain("redis://", dnsEndPoint.Host, StringComparison.Ordinal);
        return dnsEndPoint;
    }
}
