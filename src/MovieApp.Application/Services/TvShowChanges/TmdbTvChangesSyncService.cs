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
    ILogger<TmdbTvChangesSyncService> logger) : ITmdbTvChangesSyncService
{
    public Task<TmdbChangesSyncResult> SyncAsync(
        DateTime? utcNow = null,
        CancellationToken cancellationToken = default) =>
        TmdbChangesSyncCoordinator.SyncAsync(
            TmdbChangesSyncCheckpointKeys.Tv,
            tmdbTvChangesProvider.GetTvChangesPageAsync,
            relevanceRepository.GetRelevantTvShowIdsByTmdbIdAsync,
            targetedRefreshService.RefreshRelevantShowAsync,
            checkpointRepository,
            logger,
            "tv",
            utcNow,
            cancellationToken);
}
