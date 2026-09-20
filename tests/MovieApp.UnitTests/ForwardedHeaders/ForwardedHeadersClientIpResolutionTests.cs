using MovieApp.Api.ForwardedHeaders;

namespace MovieApp.UnitTests.ForwardedHeaders;

public sealed class ForwardedHeadersClientIpResolutionTests
{
    [Fact]
    public async Task ResolvesObservedRenderCloudflareClientChainExampleOne()
    {
        var resolvedIp = await ForwardedHeadersClientIpTestSupport.ResolveClientIpAsync(
            remoteIp: "::1",
            xForwardedFor: "91.93.235.244, 172.70.246.236, 10.25.126.15");

        Assert.Equal("91.93.235.244", resolvedIp);
    }

    [Fact]
    public async Task ResolvesObservedRenderCloudflareClientChainExampleTwo()
    {
        var resolvedIp = await ForwardedHeadersClientIpTestSupport.ResolveClientIpAsync(
            remoteIp: "::1",
            xForwardedFor: "5.46.137.4, 172.69.182.157, 10.25.126.15");

        Assert.Equal("5.46.137.4", resolvedIp);
    }

    [Fact]
    public async Task IgnoresSpoofedForwardedForFromUntrustedPeer()
    {
        var resolvedIp = await ForwardedHeadersClientIpTestSupport.ResolveClientIpAsync(
            remoteIp: "203.0.113.99",
            xForwardedFor: "91.93.235.244, 172.70.246.236, 10.25.126.15");

        Assert.Equal("203.0.113.99", resolvedIp);
    }

    [Fact]
    public async Task PreservesInternalRenderHealthCheckWithoutForwardedHeaders()
    {
        var resolvedIp = await ForwardedHeadersClientIpTestSupport.ResolveClientIpAsync(
            remoteIp: "10.200.27.112",
            xForwardedFor: null);

        Assert.Equal("10.200.27.112", resolvedIp);
    }

    [Fact]
    public async Task ResolvesIpv4ClientAddressThroughTrustedChain()
    {
        var resolvedIp = await ForwardedHeadersClientIpTestSupport.ResolveClientIpAsync(
            remoteIp: "::1",
            xForwardedFor: "198.51.100.42, 172.70.246.236, 10.25.126.15");

        Assert.Equal("198.51.100.42", resolvedIp);
    }

    [Fact]
    public async Task ResolvesIpv6ClientAddressThroughTrustedChain()
    {
        var resolvedIp = await ForwardedHeadersClientIpTestSupport.ResolveClientIpAsync(
            remoteIp: "::1",
            xForwardedFor: "2001:db8::42, 172.70.246.236, 10.25.126.15");

        Assert.Equal("2001:db8::42", resolvedIp);
    }
}
