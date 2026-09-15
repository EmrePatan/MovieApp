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
    ILogger<TmdbMovieChangesSyncService> logger) : ITmdbMovieChangesSyncService
{
    public Task<TmdbChangesSyncResult> SyncAsync(
        DateTime? utcNow = null,
        CancellationToken cancellationToken = default) =>
        TmdbChangesSyncCoordinator.SyncAsync(
            TmdbChangesSyncCheckpointKeys.Movie,
            movieChangesProvider.GetMovieChangesPageAsync,
            relevanceRepository.GetRelevantMovieIdsByTmdbIdAsync,
            RefreshMovieAsync,
            checkpointRepository,
            logger,
            "movie",
            utcNow,
            cancellationToken);

    private Task<TmdbChangesTargetRefreshResult> RefreshMovieAsync(
        Guid movieId,
        DateOnly _,
        DateOnly __,
        CancellationToken cancellationToken) =>
        targetedRefreshService.RefreshRelevantMovieAsync(movieId, cancellationToken);
}
