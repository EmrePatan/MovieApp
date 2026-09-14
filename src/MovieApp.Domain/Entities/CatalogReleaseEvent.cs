using MovieApp.Domain.Enums;

namespace MovieApp.Domain.Entities;

public sealed class CatalogReleaseEvent
{
    public Guid Id { get; set; }

    public Guid? TvShowId { get; set; }

    public Guid? MovieId { get; set; }

    public CatalogReleaseEventType EventType { get; set; }

    public int SeasonNumber { get; set; }

    public int? EpisodeNumber { get; set; }

    public DateTime ReleaseAtUtc { get; set; }

    public DateTime DetectedAtUtc { get; set; }

    public CatalogReleaseEventSource Source { get; set; }

    public string DedupeKey { get; set; } = string.Empty;

    public TvShow? TvShow { get; set; }

    public Movie? Movie { get; set; }

    public ICollection<UserReleaseNotificationEvent> NotificationEvents { get; set; } = [];
}
