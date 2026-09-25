using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace MovieApp.Application.Services.Keywords;

public sealed class BackgroundCatalogKeywordReadPathScheduler(
    IServiceScopeFactory scopeFactory,
    ILogger<BackgroundCatalogKeywordReadPathScheduler> logger) : ICatalogKeywordReadPathScheduler
{
    public void ScheduleMovie(Guid movieId) =>
        Schedule(
            movieId,
            "movie",
            (service, id, cancellationToken) => service.TryEnrichMovieKeywordsAsync(
                id,
                refreshKeywords: false,
                cancellationToken: cancellationToken));

    public void ScheduleTvShow(Guid tvShowId) =>
        Schedule(
            tvShowId,
            "tv",
            (service, id, cancellationToken) => service.TryEnrichTvShowKeywordsAsync(
                id,
                refreshKeywords: false,
                cancellationToken: cancellationToken));

    private void Schedule(
        Guid catalogId,
        string contentType,
        Func<ICatalogKeywordIngestionService, Guid, CancellationToken, Task> enrich)
    {
        _ = Task.Run(
            async () =>
            {
                try
                {
                    using var scope = scopeFactory.CreateScope();
                    var service = scope.ServiceProvider.GetRequiredService<ICatalogKeywordIngestionService>();
                    await enrich(service, catalogId, CancellationToken.None);
                }
                catch (Exception exception)
                {
                    CatalogKeywordReadPathSchedulerLogMessages.LogBackgroundEnrichmentFailed(
                        logger,
                        contentType,
                        catalogId,
                        exception);
                }
            },
            CancellationToken.None);
    }
}

internal static partial class CatalogKeywordReadPathSchedulerLogMessages
{
    [LoggerMessage(
        EventId = 1,
        Level = LogLevel.Warning,
        Message = "Background keyword enrichment failed for {ContentType} {CatalogId}.")]
    public static partial void LogBackgroundEnrichmentFailed(
        ILogger logger,
        string contentType,
        Guid catalogId,
        Exception exception);
}
