namespace MovieApp.Application.Models.Home;

public sealed record HotThisWeekTrendingSnapshotRefreshResult(
    bool SnapshotUpdated,
    int ProviderItemCount,
    int MappedItemCount,
    int SkippedItemCount);
