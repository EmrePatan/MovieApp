using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using MovieApp.Application.Abstractions.Persistence;
using MovieApp.Application.Abstractions.Providers;
using MovieApp.Application.Models.Changes;
using MovieApp.Application.Services.Changes;

namespace MovieApp.Application.Services.MovieChanges;

public sealed class TmdbMovieChangesSyncService(
    ITmdbMovieChangesProvider movieChangesProvider,
    ITmdbTvChangesSyncCheckpointRepository checkpointRepository,
    ICatalogChangesRelevanceRepository relevanceRepository,
    IMovieChangesTargetedRefreshService targetedRefreshService,
    IServiceScopeFactory scopeFactory,
    ILogger<TmdbMovieChangesSyncService> logger) : ITmdbMovieChangesSyncService
{
    public async Task<TmdbChangesSyncResult> SyncAsync(
        DateTime? utcNow = null,
        CancellationToken cancellationToken = default)
    {
        return await TmdbChangesSyncCoordinator.SyncAsync(
            TmdbChangesSyncCheckpointKeys.Movie,
            movieChangesProvider.GetMovieChangesPageAsync,
            relevanceRepository.GetMovieChangesRefreshMapsForTmdbIdsAsync,
            RefreshMovieAsync,
            checkpointRepository,
            logger,
            "movie",
            utcNow,
            cancellationToken);
    }

    private async Task<TmdbChangesTargetRefreshResult> RefreshMovieAsync(
        Guid movieId,
        DateOnly _,
        DateOnly __,
        CancellationToken cancellationToken)
    {
        await using var scope = scopeFactory.CreateAsyncScope();
        var refreshService = scope.ServiceProvider.GetRequiredService<IMovieChangesTargetedRefreshService>();
        return await refreshService.RefreshRelevantMovieAsync(movieId, cancellationToken);
    }
}
