using System.Net;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Options;

namespace MovieApp.Infrastructure.Configuration;

public sealed class ForwardedHeadersOptionsValidator(IHostEnvironment hostEnvironment)
    : IValidateOptions<ForwardedHeadersOptionsConfig>
{
    public ValidateOptionsResult Validate(string? name, ForwardedHeadersOptionsConfig options)
    {
        if (!hostEnvironment.IsProduction())
        {
            return ValidateOptionsResult.Success;
        }

        if (!options.Enabled)
        {
            return ValidateOptionsResult.Success;
        }

        var hasKnownProxy = options.KnownProxies.Any(static proxy =>
            !string.IsNullOrWhiteSpace(proxy) && IPAddress.TryParse(proxy.Trim(), out _));

        var hasKnownNetwork = options.KnownNetworks.Any(static network =>
            !string.IsNullOrWhiteSpace(network) && TryParseCidr(network.Trim(), out _));

        if (!hasKnownProxy && !hasKnownNetwork)
        {
            return ValidateOptionsResult.Fail(
                "ForwardedHeaders:Enabled is true in Production but no trusted KnownProxies or KnownNetworks are configured. " +
                "Configure explicit load-balancer proxy IPs or CIDR ranges to avoid trusting arbitrary X-Forwarded-* headers.");
        }

        return ValidateOptionsResult.Success;
    }

    private static bool TryParseCidr(string value, out System.Net.IPNetwork network)
    {
        network = default!;
        var parts = value.Split('/', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
        if (parts.Length != 2 ||
            !IPAddress.TryParse(parts[0], out var prefix) ||
            !int.TryParse(parts[1], out var prefixLength))
        {
            return false;
        }

        network = new System.Net.IPNetwork(prefix, prefixLength);
        return true;
    }
}
