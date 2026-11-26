namespace MovieApp.Domain.Entities;

/// <summary>
/// Streaming platforms the user subscribes to, as provider ids. A missing row means none selected.
/// </summary>
public sealed class UserWatchPlatformPreference
{
    public Guid UserId { get; set; }

    public List<int> ProviderIds { get; set; } = [];

    public DateTime UpdatedAtUtc { get; set; }

    public User User { get; set; } = null!;
}
