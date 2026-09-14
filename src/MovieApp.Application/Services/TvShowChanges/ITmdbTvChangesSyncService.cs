using MovieApp.Application.Models.TvShowChanges;

namespace MovieApp.Application.Services.TvShowChanges;

public interface ITmdbTvChangesSyncService
{
    Task<TmdbTvChangesSyncResult> SyncAsync(
        DateTime? utcNow = null,
        CancellationToken cancellationToken = default);
}
