namespace MovieApp.Domain.Entities;

/// <summary>Fetch state of the leaving-soon list for one region.</summary>
public sealed class StreamingLeavingRegion
{
    public string Region { get; set; } = string.Empty;

    public DateTime? LastAttemptAtUtc { get; set; }

    public DateTime? LastSuccessAtUtc { get; set; }

    public int LastFetchRequestCount { get; set; }

    /// <summary>False when the last stored list was cut by a request limit or an error.</summary>
    public bool IsComplete { get; set; }

    /// <summary>Subscription services of the region, cached so a fetch does not ask for them every time.</summary>
    public string? ServicesJson { get; set; }

    public DateTime? ServicesFetchedAtUtc { get; set; }
}
