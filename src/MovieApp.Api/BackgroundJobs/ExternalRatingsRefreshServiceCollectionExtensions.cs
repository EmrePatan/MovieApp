using MovieApp.Application.Abstractions.ExternalRatings;
using MovieApp.Application.Configuration;
using MovieApp.Infrastructure.ExternalRatings;

namespace MovieApp.Api.BackgroundJobs;

public static class ExternalRatingsRefreshServiceCollectionExtensions
{
    public static IServiceCollection AddExternalRatingsRefreshProcessing(
        this IServiceCollection services,
        IConfiguration configuration,
        IHostEnvironment hostEnvironment)
    {
        var backgroundJobsEnabled = configuration
            .GetSection(BackgroundJobsOptions.SectionName)
            .GetValue<bool>(nameof(BackgroundJobsOptions.Enabled));

        if (hostEnvironment.IsEnvironment("Testing") || !backgroundJobsEnabled)
        {
            services.AddSingleton<IExternalRatingsRefreshJobEnqueuer, SynchronousExternalRatingsRefreshJobEnqueuer>();
            return services;
        }

        services.AddScoped<ExternalRatingsRefreshJob>();
        services.AddSingleton<IExternalRatingsRefreshJobEnqueuer, HangfireExternalRatingsRefreshJobEnqueuer>();

        return services;
    }
}
