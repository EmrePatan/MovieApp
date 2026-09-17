namespace MovieApp.Application.Models.Insights;

public sealed record InsightsActivityEventData(
    DateTime WatchedAtUtc,
    bool IsMovie,
    int? EstimatedMinutes);
