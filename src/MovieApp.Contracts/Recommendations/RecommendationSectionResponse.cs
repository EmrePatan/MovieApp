namespace MovieApp.Contracts.Recommendations;

public sealed record RecommendationSectionResponse(
    string Key,
    string Title,
    IReadOnlyList<RecommendationItemResponse> Items);
