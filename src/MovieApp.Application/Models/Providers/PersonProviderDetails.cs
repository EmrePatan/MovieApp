namespace MovieApp.Application.Models.Providers;

public sealed record PersonProviderDetails(
    int TmdbId,
    string Name,
    string? ProfilePath,
    string? Biography,
    DateOnly? Birthday,
    DateOnly? Deathday,
    string? PlaceOfBirth,
    string? KnownForDepartment,
    IReadOnlyList<PersonFilmographyCredit> FilmographyCredits);

public sealed record PersonFilmographyCredit(
    string MediaType,
    int TmdbId,
    string Title,
    string? PosterPath,
    string? Character,
    DateOnly? ReleaseDate,
    decimal Popularity,
    decimal VoteAverage);
