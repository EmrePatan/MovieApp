using System.Net;
using Microsoft.AspNetCore.HttpOverrides;
using Microsoft.Extensions.Options;
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
            ConfigureForwardedHeadersOptions(forwardedHeadersOptions, options);
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

    internal static void ConfigureForwardedHeadersOptions(
        ForwardedHeadersOptions forwardedHeadersOptions,
        ForwardedHeadersOptionsConfig options)
    {
        forwardedHeadersOptions.ForwardedHeaders =
            HttpOverridesForwardedHeaders.XForwardedFor | HttpOverridesForwardedHeaders.XForwardedProto;

        forwardedHeadersOptions.KnownIPNetworks.Clear();
        forwardedHeadersOptions.KnownProxies.Clear();
        forwardedHeadersOptions.ForwardLimit = null;

        if (options.UseRenderProxyTrustDefaults)
        {
            RenderForwardedHeadersTrust.ApplyDefaultKnownNetworks(forwardedHeadersOptions);
        }

        foreach (var proxy in options.KnownProxies)
        {
            if (IPAddress.TryParse(proxy, out var parsedProxy))
            {
                forwardedHeadersOptions.KnownProxies.Add(parsedProxy);
            }
        }

        foreach (var network in options.KnownNetworks)
        {
            if (ForwardedHeadersNetworkParser.TryParseCidr(network, out var parsedNetwork))
            {
                forwardedHeadersOptions.KnownIPNetworks.Add(parsedNetwork);
            }
        }
    }

}
