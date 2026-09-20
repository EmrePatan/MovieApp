using System.Net;
using Microsoft.Extensions.Options;

namespace MovieApp.Api.Diagnostics;

// TEMPORARY (#27 M4): remove after Render client-IP chain verification.
internal sealed class TemporaryClientIpChainDiagnosticMiddleware(
    RequestDelegate next,
    ILogger<TemporaryClientIpChainDiagnosticMiddleware> logger,
    IOptions<TemporaryClientIpChainDiagnosticOptions> options)
{
    public async Task InvokeAsync(HttpContext context)
    {
        if (options.Value.Enabled)
        {
            LogClientIpChain(context);
        }

        await next(context);
    }

    private void LogClientIpChain(HttpContext context)
    {
        var remoteIpAddress = FormatIpAddress(context.Connection.RemoteIpAddress);
        var forwardedFor = ForwardedForHeaderParser.Parse(
            context.Request.Headers["X-Forwarded-For"].FirstOrDefault());
        var cfConnectingIp = context.Request.Headers["CF-Connecting-IP"].FirstOrDefault()?.Trim();
        var cfRay = context.Request.Headers["CF-Ray"].FirstOrDefault()?.Trim();
        var path = context.Request.Path.Value ?? "/";

        TemporaryClientIpChainDiagnosticLogMessages.LogClientIpChain(
            logger,
            remoteIpAddress,
            forwardedFor.HopCount,
            forwardedFor.Chain,
            string.IsNullOrWhiteSpace(cfConnectingIp) ? null : cfConnectingIp,
            string.IsNullOrWhiteSpace(cfRay) ? null : cfRay,
            path);
    }

    private static string? FormatIpAddress(IPAddress? address)
    {
        if (address is null)
        {
            return null;
        }

        if (address.IsIPv4MappedToIPv6)
        {
            address = address.MapToIPv4();
        }

        return address.ToString();
    }
}
