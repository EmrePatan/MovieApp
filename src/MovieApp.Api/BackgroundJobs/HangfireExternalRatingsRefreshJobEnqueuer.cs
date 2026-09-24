using Hangfire;
using MovieApp.Application.Abstractions.ExternalRatings;
using MovieApp.Domain.Enums;

namespace MovieApp.Api.BackgroundJobs;

public sealed class HangfireExternalRatingsRefreshJobEnqueuer(IBackgroundJobClient backgroundJobClient)
    : IExternalRatingsRefreshJobEnqueuer
{
    public void EnqueueRefresh(CatalogContentType mediaType, int tmdbId) =>
        backgroundJobClient.Enqueue<ExternalRatingsRefreshJob>(
            job => job.ExecuteAsync(mediaType, tmdbId));
}
