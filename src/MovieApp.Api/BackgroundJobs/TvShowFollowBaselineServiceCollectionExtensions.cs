using MovieApp.Application.Abstractions.TvShowFollows;
using MovieApp.Application.Configuration;
using MovieApp.Infrastructure.TvShowFollows;

namespace MovieApp.Api.BackgroundJobs;

public static class TvShowFollowBaselineServiceCollectionExtensions
{
    public static IServiceCollection AddTvShowFollowBaselineProcessing(
        this IServiceCollection services,
        IConfiguration configuration,
        IHostEnvironment hostEnvironment)
    {
        var backgroundJobsEnabled = configuration
            .GetSection(BackgroundJobsOptions.SectionName)
            .GetValue<bool>(nameof(BackgroundJobsOptions.Enabled));

        if (hostEnvironment.IsEnvironment("Testing") || !backgroundJobsEnabled)
        {
            services.AddSingleton<ITvShowFollowBaselineJobEnqueuer, SynchronousTvShowFollowBaselineJobEnqueuer>();
            return services;
        }

        services.AddScoped<TvShowFollowBaselineJob>();
        services.AddSingleton<ITvShowFollowBaselineJobEnqueuer, HangfireTvShowFollowBaselineJobEnqueuer>();

        return services;
    }
}
