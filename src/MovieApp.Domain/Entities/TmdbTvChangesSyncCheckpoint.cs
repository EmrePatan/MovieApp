namespace MovieApp.Domain.Entities;

public sealed class TmdbTvChangesSyncCheckpoint
{
    public const string DefaultCheckpointKey = "tmdb-tv-changes";

    public string CheckpointKey { get; set; } = DefaultCheckpointKey;

    public DateOnly? LastCompletedEndDate { get; set; }

    public DateTime? LastCompletedAtUtc { get; set; }

    public DateTime UpdatedAtUtc { get; set; }
}
