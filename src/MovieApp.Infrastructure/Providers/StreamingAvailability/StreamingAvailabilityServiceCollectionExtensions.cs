using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using MovieApp.Application.Abstractions.Persistence;
using MovieApp.Application.Abstractions.Providers;
using MovieApp.Application.Configuration;
using MovieApp.Infrastructure.Persistence.Repositories;

namespace MovieApp.Infrastructure.Providers.StreamingAvailability;

internal static class StreamingAvailabilityServiceCollectionExtensions
{
    internal static IServiceCollection AddStreamingAvailability(
        this IServiceCollection services,
        IConfiguration configuration)
    {
        services.AddOptions<StreamingAvailabilityOptions>()
            .Bind(configuration.GetSection(StreamingAvailabilityOptions.SectionName));

        services.AddScoped<IStreamingLeavingRepository, StreamingLeavingRepository>();
        services.AddScoped<IStreamingLeavingTitleResolver, StreamingLeavingTitleResolver>();

        services.AddHttpClient<StreamingAvailabilityApiProvider>((serviceProvider, client) =>
        {
            var options = serviceProvider.GetRequiredService<IOptions<StreamingAvailabilityOptions>>().Value;
            client.BaseAddress = new Uri(options.BaseUrl.TrimEnd('/') + "/", UriKind.Absolute);
            client.Timeout = TimeSpan.FromSeconds(Math.Max(1, options.TimeoutSeconds));
            if (!string.IsNullOrWhiteSpace(options.ApiKey))
            {
                client.DefaultRequestHeaders.Add("X-API-Key", options.ApiKey.Trim());
            }
        });
        services.AddScoped<FakeStreamingAvailabilityProvider>();
        services.AddScoped<IStreamingAvailabilityProvider>(serviceProvider =>
            serviceProvider.GetRequiredService<IOptions<StreamingAvailabilityOptions>>().Value.UsesFakeProvider
                ? serviceProvider.GetRequiredService<FakeStreamingAvailabilityProvider>()
                : serviceProvider.GetRequiredService<StreamingAvailabilityApiProvider>());

        return services;
    }
}
