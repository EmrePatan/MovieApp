using System.Net;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.HttpOverrides;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using MovieApp.Api.RateLimiting;
using MovieApp.Infrastructure.Configuration;

namespace MovieApp.Api.ForwardedHeaders;

internal static class ForwardedHeadersClientIpTestSupport
{
    internal static async Task<string> ResolveClientIpAsync(string remoteIp, string? xForwardedFor)
    {
        var forwardedHeadersOptions = new ForwardedHeadersOptions();
        ForwardedHeadersExtensions.ConfigureForwardedHeadersOptions(
            forwardedHeadersOptions,
            new ForwardedHeadersOptionsConfig { UseRenderProxyTrustDefaults = true });

        string? resolvedIp = null;
        var middleware = new ForwardedHeadersMiddleware(
            innerContext =>
            {
                resolvedIp = ClientIpResolver.GetClientIpAddress(innerContext);
                return Task.CompletedTask;
            },
            NullLoggerFactory.Instance,
            Options.Create(forwardedHeadersOptions));

        var context = new DefaultHttpContext
        {
            Connection = { RemoteIpAddress = IPAddress.Parse(remoteIp) }
        };
        if (xForwardedFor is not null)
        {
            context.Request.Headers["X-Forwarded-For"] = xForwardedFor;
        }

        await middleware.Invoke(context);
        return resolvedIp ?? throw new InvalidOperationException("Client IP was not resolved.");
    }
}
