using MovieApp.Domain.Enums;

namespace MovieApp.Domain.Entities;

public sealed class TvShowCatalogSyncState
{
    public Guid TvShowId { get; set; }

    public DateTime? LastRefreshedAtUtc { get; set; }

    public DateOnly? LastChangeSignalDate { get; set; }

    public TvShowCatalogRefreshReason? LastRefreshReason { get; set; }

    public DateTime? NextHotCheckAtUtc { get; set; }

    public DateTime? LastUpcomingEpisodeSyncAtUtc { get; set; }

    /// <summary>Set while the latest upcoming-episode sync failed; cleared by a success.</summary>
    public DateTime? LastUpcomingEpisodeSyncFailedAtUtc { get; set; }

    public DateTime UpdatedAtUtc { get; set; }

    public TvShow TvShow { get; set; } = null!;
}
