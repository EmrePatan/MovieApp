namespace MovieApp.Contracts.Insights;

public sealed record InsightsActivitySummaryResponse(
    int TotalActiveDays,
    DayOfWeek? MostActiveWeekday,
    int? LongestStreakDays,
    int CurrentWeekTotal,
    int PreviousWeekTotal);
