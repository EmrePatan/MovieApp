using System.Net;

namespace MovieApp.Api.Diagnostics;

// TEMPORARY (#27 M4): remove after Render client-IP chain verification.
internal static class ForwardedForHeaderParser
{
    internal static ForwardedForHeaderParseResult Parse(string? headerValue)
    {
        if (string.IsNullOrWhiteSpace(headerValue))
        {
            return new ForwardedForHeaderParseResult(0, []);
        }

        var hops = headerValue
            .Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .Where(static hop => !string.IsNullOrWhiteSpace(hop))
            .Select(NormalizeHop)
            .ToArray();

        return new ForwardedForHeaderParseResult(hops.Length, hops);
    }

    private static string NormalizeHop(string hop)
    {
        if (IPAddress.TryParse(hop, out var address) && address.IsIPv4MappedToIPv6)
        {
            return address.MapToIPv4().ToString();
        }

        return hop;
    }
}

internal readonly record struct ForwardedForHeaderParseResult(int HopCount, string[] Chain);
