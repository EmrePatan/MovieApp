namespace MovieApp.Contracts.People;

public sealed record PersonDetailResponse(
    Guid Id,
    int TmdbId,
    string Name,
    string? ProfileImagePath,
    string? Biography,
    string? Birthday,
    string? Deathday,
    string? PlaceOfBirth,
    string? KnownForDepartment,
    IReadOnlyList<PersonFilmographyEntryResponse> Filmography);

public sealed record PersonFilmographyEntryResponse(
    string MediaType,
    Guid? CatalogId,
    int TmdbId,
    string Title,
    string? PosterPath,
    string? Character,
    string? ReleaseDate);
