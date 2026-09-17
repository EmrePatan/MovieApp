namespace MovieApp.Contracts.Insights;

public sealed record InsightsMilestoneResponse(
    string Id,
    string Category,
    string Title,
    int CurrentValue,
    int TargetValue,
    bool Achieved,
    DateTime? AchievedAt);
