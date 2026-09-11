namespace MovieApp.Contracts.Recommendations;

public sealed record RecommendationHomeResponse(
    IReadOnlyList<RecommendationSectionResponse> Sections);
