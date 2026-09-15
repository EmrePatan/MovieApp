namespace MovieApp.Application.Models.Providers;

public sealed record TrendingWeekProviderItem(
    string MediaType,
    int TmdbId,
    string Title,
    string? OriginalTitle,
    string? Overview,
    DateOnly? ReleaseDate,
    string? PosterPath,
    string? BackdropPath,
    decimal VoteAverage,
    int VoteCount);
