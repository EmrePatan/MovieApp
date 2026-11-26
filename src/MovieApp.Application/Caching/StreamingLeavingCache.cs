using MovieApp.Application.Models.StreamingLeaving;
using MovieApp.Domain.Entities;

namespace MovieApp.Application.Caching;

/// <summary>
/// A region's stored list in front of the database. A fetch removes the entry, so new data shows at once.
/// </summary>
public static class StreamingLeavingCache
{
    public static readonly TimeSpan Ttl = TimeSpan.FromHours(24);

    public static string Key(string region) => $"streaming-leaving:list:{region}";
}

public sealed class StreamingLeavingCacheEntry
{
    public IReadOnlyList<StreamingLeavingRow> Rows { get; set; } = [];

    public StreamingLeavingRegion? State { get; set; }
}
