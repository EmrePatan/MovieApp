using MovieApp.Application.Models.WatchHistory;

namespace MovieApp.Application.Services.WatchHistory;

public interface IWatchStatusBatchService
{
    Task<WatchStatusBatchResult> GetAsync(
        IReadOnlyList<Guid>? movieIds,
        IReadOnlyList<Guid>? tvShowIds,
        CancellationToken cancellationToken = default);
}
