using MovieApp.Application.Models.Changes;

namespace MovieApp.Application.Services.MovieChanges;

public interface ITmdbMovieChangesSyncService
{
    Task<TmdbChangesSyncResult> SyncAsync(
        DateTime? utcNow = null,
        CancellationToken cancellationToken = default);
}
