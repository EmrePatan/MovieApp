using MovieApp.Application.Models.Home;

namespace MovieApp.Application.Services.Home;

public interface IHotThisWeekTrendingSnapshotService
{
    Task<HotThisWeekTrendingSnapshotEntry?> GetSnapshotAsync(CancellationToken cancellationToken = default);

    Task<HotThisWeekTrendingSnapshotRefreshResult> RefreshAsync(CancellationToken cancellationToken = default);
}
