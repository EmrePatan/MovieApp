namespace MovieApp.Domain.Entities;

/// <summary>
/// A title whose TMDB change refresh failed in a window the checkpoint has already passed.
/// Rows that run out of attempts stay here so they can be looked at.
/// </summary>
public sealed class TmdbChangesFailedTarget
{
    public string CheckpointKey { get; set; } = string.Empty;

    /// <summary>Catalog id of the movie or TV show.</summary>
    public Guid TargetId { get; set; }

    public DateOnly ChangeDate { get; set; }

    /// <summary>Retries made after the failure that created the row.</summary>
    public int RetryCount { get; set; }

    public DateTime LastFailedAtUtc { get; set; }
}
