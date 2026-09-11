namespace MovieApp.Application.Models.Recommendations;

public sealed record RecommendationCriteria(
    RecommendationContentType Type,
    int Page,
    int PageSize);
