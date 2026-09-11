namespace MovieApp.Application.Models.Search;

public sealed record SearchItem(
    Guid Id,
    string Type,
    string Title,
    string? OriginalTitle,
    string? Overview,
    string? PosterUrl,
    string? BackdropUrl,
    DateOnly? ReleaseDate,
    decimal VoteAverage,
    int VoteCount,
    int? Year);
