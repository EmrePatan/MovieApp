namespace MovieApp.Application.Models.People;

public sealed record PersonDetailResult(
    Guid Id,
    int TmdbId,
    string Name,
    string? ProfileImagePath,
    string? Biography,
    DateOnly? Birthday,
    DateOnly? Deathday,
    string? PlaceOfBirth,
    string? KnownForDepartment,
    IReadOnlyList<PersonFilmographyEntryResult> Filmography);

public sealed record PersonFilmographyEntryResult(
    string MediaType,
    Guid? CatalogId,
    int TmdbId,
    string Title,
    string? PosterPath,
    string? Character,
    DateOnly? ReleaseDate);
