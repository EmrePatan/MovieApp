using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using MovieApp.Application.Abstractions.Persistence;
using MovieApp.Application.Abstractions.Providers;
using MovieApp.Application.Models.Changes;
using MovieApp.Application.Services.Changes;

namespace MovieApp.Application.Services.TvShowChanges;

public sealed class TmdbTvChangesSyncService(
    ITmdbTvChangesProvider tmdbTvChangesProvider,
    ITmdbTvChangesSyncCheckpointRepository checkpointRepository,
    ICatalogChangesRelevanceRepository relevanceRepository,
    ITvShowChangesTargetedRefreshService targetedRefreshService,
    ILogger<TmdbTvChangesSyncService> logger,
    IServiceScopeFactory? scopeFactory = null) : ITmdbTvChangesSyncService
{
    public async Task<TmdbChangesSyncResult> SyncAsync(
        DateTime? utcNow = null,
        CancellationToken cancellationToken = default)
    {
        return await TmdbChangesSyncCoordinator.SyncAsync(
            TmdbChangesSyncCheckpointKeys.Tv,
            tmdbTvChangesProvider.GetTvChangesPageAsync,
            relevanceRepository.GetTvShowChangesRefreshMapsForTmdbIdsAsync,
            RefreshShowAsync,
            checkpointRepository,
            logger,
            "tv",
            utcNow,
            cancellationToken);
    }
    private async Task<TmdbChangesTargetRefreshResult> RefreshShowAsync(
        Guid tvShowId,
        DateOnly boundaryDate,
        DateOnly changeSignalDate,
        CancellationToken cancellationToken)
    {
        if (scopeFactory is null)
        {
            return await targetedRefreshService.RefreshRelevantShowAsync(
                tvShowId,
                boundaryDate,
                changeSignalDate,
                cancellationToken);
        }

        await using var scope = scopeFactory.CreateAsyncScope();
        var refreshService = scope.ServiceProvider.GetRequiredService<ITvShowChangesTargetedRefreshService>();
        return await refreshService.RefreshRelevantShowAsync(
            tvShowId,
            boundaryDate,
            changeSignalDate,
            cancellationToken);
    }
}
