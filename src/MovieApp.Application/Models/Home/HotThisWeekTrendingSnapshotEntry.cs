using MovieApp.Application.Models.Search;

namespace MovieApp.Application.Models.Home;

public sealed class HotThisWeekTrendingSnapshotEntry
{
    public required DateTimeOffset RefreshedAt { get; init; }

    public required IReadOnlyList<SearchItem> Items { get; init; }
}
