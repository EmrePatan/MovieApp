using System.Net;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Options;

namespace MovieApp.Infrastructure.Configuration;

public sealed class ForwardedHeadersOptionsValidator(IHostEnvironment hostEnvironment)
    : IValidateOptions<ForwardedHeadersOptionsConfig>
{
    public ValidateOptionsResult Validate(string? name, ForwardedHeadersOptionsConfig options)
    {
        if (!hostEnvironment.IsProduction() || !options.Enabled)
        {
            return ValidateOptionsResult.Success;
        }

        if (options.UseRenderProxyTrustDefaults || HasExplicitTrustedProxies(options))
        {
            return ValidateOptionsResult.Success;
        }

        return ValidateOptionsResult.Fail(
            "ForwardedHeaders:Enabled is true in Production but neither UseRenderProxyTrustDefaults nor explicit KnownProxies/KnownNetworks are configured. " +
            "Enable UseRenderProxyTrustDefaults for Render/Cloudflare proxy chains or configure explicit trusted proxy IPs/CIDR ranges.");
    }

    private static bool HasExplicitTrustedProxies(ForwardedHeadersOptionsConfig options)
    {
        var hasKnownProxy = options.KnownProxies.Any(static proxy =>
            !string.IsNullOrWhiteSpace(proxy) && IPAddress.TryParse(proxy.Trim(), out _));

        var hasKnownNetwork = options.KnownNetworks.Any(static network =>
            !string.IsNullOrWhiteSpace(network) && TryParseCidr(network.Trim(), out _));

        return hasKnownProxy || hasKnownNetwork;
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
