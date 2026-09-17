namespace MovieApp.Contracts.Insights;

public sealed record InsightsTasteResponse(
    IReadOnlyList<InsightsTasteGenreResponse> Genres);
