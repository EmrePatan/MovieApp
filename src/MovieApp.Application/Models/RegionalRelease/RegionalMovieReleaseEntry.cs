using MovieApp.Domain.Enums;

namespace MovieApp.Application.Models.RegionalRelease;

public sealed record RegionalMovieReleaseEntry(
    string Region,
    DateOnly ReleaseDate,
    TmdbReleaseType Type,
    string? Certification,
    int SourceIndex);
