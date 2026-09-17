namespace MovieApp.Contracts.Insights;

public sealed record InsightsTasteGenreResponse(
    Guid GenreId,
    string Name,
    decimal Weight,
    decimal SharePercent);
