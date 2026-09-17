namespace MovieApp.Application.Models.Insights;

public sealed record InsightsDnaTitleData(
    int? ReleaseYear,
    IReadOnlyList<InsightsDnaGenreData> Genres);
