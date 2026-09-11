namespace MovieApp.Application.Models.Recommendations;

public sealed record RecommendationSection(
    string Key,
    string Title,
    IReadOnlyList<RecommendationItem> Items);
