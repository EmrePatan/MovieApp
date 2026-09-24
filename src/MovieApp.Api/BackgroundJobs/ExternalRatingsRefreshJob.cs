using Hangfire;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using MovieApp.Application.Services.ExternalRatings;
using MovieApp.Domain.Enums;

namespace MovieApp.Api.BackgroundJobs;

public sealed class ExternalRatingsRefreshJob(
    IServiceScopeFactory serviceScopeFactory,
    ILogger<ExternalRatingsRefreshJob> logger)
{
    public const string QueueName = "external-ratings";

    [Queue(QueueName)]
    [AutomaticRetry(Attempts = 1)]
    public Task ExecuteAsync(CatalogContentType mediaType, int tmdbId) =>
        BackgroundJobOperationalRunner.RunAsync(
            logger,
            $"external-ratings:{mediaType}:{tmdbId}",
            () => ExecuteInternalAsync(mediaType, tmdbId));

    private async Task ExecuteInternalAsync(CatalogContentType mediaType, int tmdbId)
    {
        using var scope = serviceScopeFactory.CreateScope();
        var refreshService = scope.ServiceProvider.GetRequiredService<IExternalRatingsRefreshService>();
        await refreshService.RefreshAsync(mediaType, tmdbId, CancellationToken.None);
    }
}
