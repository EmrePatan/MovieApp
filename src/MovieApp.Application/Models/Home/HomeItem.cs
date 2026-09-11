namespace MovieApp.Application.Models.Home;

public sealed record HomeItem(
    Guid Id,
    string ContentType,
    string Title,
    string? OriginalTitle,
    string? PosterUrl,
    string? BackdropUrl,
    DateOnly? ReleaseDate,
    decimal VoteAverage,
    int VoteCount);
