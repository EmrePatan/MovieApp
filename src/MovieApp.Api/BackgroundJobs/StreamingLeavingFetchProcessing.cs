using Hangfire;
using MovieApp.Application.Abstractions.Providers;
using MovieApp.Application.Configuration;
using MovieApp.Application.Services.StreamingLeaving;

namespace MovieApp.Api.BackgroundJobs;

public sealed class StreamingLeavingFetchJob(IStreamingLeavingFetchService fetchService)
{
    // Hangfire retries would spend quota again; the service has its own cooldown.
    [AutomaticRetry(Attempts = 0)]
    public Task ExecuteAsync(string region) => fetchService.FetchAsync(region, CancellationToken.None);
}

public sealed class HangfireStreamingLeavingFetchJobEnqueuer(IBackgroundJobClient backgroundJobClient)
    : IStreamingLeavingFetchJobEnqueuer
{
    public void EnqueueFetch(string region) =>
        backgroundJobClient.Enqueue<StreamingLeavingFetchJob>(job => job.ExecuteAsync(region));
}

/// <summary>Used without Hangfire: runs the fetch on the thread pool.</summary>
public sealed partial class InProcessStreamingLeavingFetchJobEnqueuer(
    IServiceScopeFactory serviceScopeFactory,
    ILogger<InProcessStreamingLeavingFetchJobEnqueuer> logger) : IStreamingLeavingFetchJobEnqueuer
{
    public void EnqueueFetch(string region) =>
        _ = Task.Run(async () =>
        {
            try
            {
                await using var scope = serviceScopeFactory.CreateAsyncScope();
                await scope.ServiceProvider.GetRequiredService<IStreamingLeavingFetchService>()
                    .FetchAsync(region, CancellationToken.None);
            }
            catch (Exception exception)
            {
                LogFetchCrashed(logger, exception, region);
            }
        });

    [LoggerMessage(Level = LogLevel.Warning, Message = "Leaving-soon fetch crashed for {Region}.")]
    private static partial void LogFetchCrashed(ILogger logger, Exception exception, string region);
}

public static class StreamingLeavingFetchServiceCollectionExtensions
{
    public static IServiceCollection AddStreamingLeavingFetchProcessing(
        this IServiceCollection services,
        IConfiguration configuration,
        IHostEnvironment hostEnvironment)
    {
        var backgroundJobsEnabled = configuration
            .GetSection(BackgroundJobsOptions.SectionName)
            .GetValue<bool>(nameof(BackgroundJobsOptions.Enabled));

        if (hostEnvironment.IsEnvironment("Testing") || !backgroundJobsEnabled)
        {
            services.AddSingleton<IStreamingLeavingFetchJobEnqueuer, InProcessStreamingLeavingFetchJobEnqueuer>();
            return services;
        }

        services.AddScoped<StreamingLeavingFetchJob>();
        services.AddSingleton<IStreamingLeavingFetchJobEnqueuer, HangfireStreamingLeavingFetchJobEnqueuer>();

        return services;
    }
}
