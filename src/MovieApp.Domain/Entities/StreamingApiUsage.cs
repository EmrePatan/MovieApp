namespace MovieApp.Domain.Entities;

/// <summary>Requests sent to the streaming availability service in one quota period for one region.</summary>
public sealed class StreamingApiUsage
{
    public DateOnly PeriodStart { get; set; }

    public string Region { get; set; } = string.Empty;

    public int RequestCount { get; set; }
}
