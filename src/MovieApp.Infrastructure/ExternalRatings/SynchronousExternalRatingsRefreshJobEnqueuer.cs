using Microsoft.Extensions.DependencyInjection;
using MovieApp.Application.Abstractions.ExternalRatings;
using MovieApp.Application.Services.ExternalRatings;
using MovieApp.Domain.Enums;

namespace MovieApp.Infrastructure.ExternalRatings;

public sealed class SynchronousExternalRatingsRefreshJobEnqueuer(
    IServiceScopeFactory serviceScopeFactory) : IExternalRatingsRefreshJobEnqueuer
{
    public void EnqueueRefresh(CatalogContentType mediaType, int tmdbId)
    {
        _ = Task.Run(async () =>
        {
            try
            {
                using var scope = serviceScopeFactory.CreateScope();
                var refreshService = scope.ServiceProvider.GetRequiredService<IExternalRatingsRefreshService>();
                await refreshService.RefreshAsync(mediaType, tmdbId, CancellationToken.None);
            }
            catch
            {
                // Background stale refresh failures are non-critical.
            }
        });
    }
}
