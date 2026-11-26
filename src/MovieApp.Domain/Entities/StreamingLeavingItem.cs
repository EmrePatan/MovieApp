using MovieApp.Domain.Enums;

namespace MovieApp.Domain.Entities;

/// <summary>One title leaving one subscription service in one region.</summary>
public sealed class StreamingLeavingItem
{
    public Guid Id { get; set; }

    public string Region { get; set; } = string.Empty;

    public string ServiceId { get; set; } = string.Empty;

    public string ServiceName { get; set; } = string.Empty;

    public CatalogContentType MediaType { get; set; }

    public int TmdbId { get; set; }

    /// <summary>Id of the movie or TV show in our catalog, depending on <see cref="MediaType"/>.</summary>
    public Guid ContentId { get; set; }

    public DateTime LeavesAtUtc { get; set; }

    public string? WatchLink { get; set; }

    public DateTime FetchedAtUtc { get; set; }
}
