using System.Net;
using Microsoft.AspNetCore.HttpOverrides;
using Microsoft.Extensions.Options;
using Microsoft.Extensions.Hosting;
using HttpOverridesForwardedHeaders = Microsoft.AspNetCore.HttpOverrides.ForwardedHeaders;
using MovieApp.Infrastructure.Configuration;

namespace MovieApp.Api.ForwardedHeaders;

internal static class ForwardedHeadersExtensions
{
    internal static IServiceCollection AddConfiguredForwardedHeaders(
        this IServiceCollection services,
        IConfiguration configuration)
    {
        services.AddOptions<ForwardedHeadersOptionsConfig>()
            .Bind(configuration.GetSection(ForwardedHeadersOptionsConfig.SectionName))
            .ValidateOnStart();

        services.AddSingleton<IValidateOptions<ForwardedHeadersOptionsConfig>, ForwardedHeadersOptionsValidator>();

        var options = configuration
            .GetSection(ForwardedHeadersOptionsConfig.SectionName)
            .Get<ForwardedHeadersOptionsConfig>() ?? new ForwardedHeadersOptionsConfig();

        if (!options.Enabled)
        {
            return services;
        }

        services.Configure<ForwardedHeadersOptions>(forwardedHeadersOptions =>
        {
            forwardedHeadersOptions.ForwardedHeaders =
                HttpOverridesForwardedHeaders.XForwardedFor | HttpOverridesForwardedHeaders.XForwardedProto;

            forwardedHeadersOptions.KnownIPNetworks.Clear();
            forwardedHeadersOptions.KnownProxies.Clear();

            foreach (var proxy in options.KnownProxies)
            {
                if (IPAddress.TryParse(proxy, out var parsedProxy))
                {
                    forwardedHeadersOptions.KnownProxies.Add(parsedProxy);
                }
            }

            foreach (var network in options.KnownNetworks)
            {
                if (TryParseCidr(network, out var parsedNetwork))
                {
                    forwardedHeadersOptions.KnownIPNetworks.Add(parsedNetwork);
                }
            }
        });

        return services;
    }

    internal static WebApplication UseConfiguredForwardedHeaders(this WebApplication app)
    {
        var options = app.Configuration
            .GetSection(ForwardedHeadersOptionsConfig.SectionName)
            .Get<ForwardedHeadersOptionsConfig>() ?? new ForwardedHeadersOptionsConfig();

        if (options.Enabled)
        {
            app.UseForwardedHeaders();
        }

        return app;
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
