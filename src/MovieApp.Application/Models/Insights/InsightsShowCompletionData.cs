namespace MovieApp.Application.Models.Insights;

public sealed record InsightsShowCompletionData(
    int TotalEpisodes,
    int WatchedEpisodes,
    DateTime? LastWatchedAtUtc,
    bool IsConcluded);
