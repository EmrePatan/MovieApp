using System.Net;

namespace MovieApp.Api.ForwardedHeaders;

internal static class ForwardedHeadersNetworkParser
{
    internal static bool TryParseCidr(string value, out IPNetwork network)
    {
        network = default!;
        var parts = value.Split('/', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
        if (parts.Length != 2 ||
            !IPAddress.TryParse(parts[0], out var prefix) ||
            !int.TryParse(parts[1], out var prefixLength))
        {
            return false;
        }

        network = new IPNetwork(prefix, prefixLength);
        return true;
    }
}
