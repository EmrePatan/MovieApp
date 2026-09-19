namespace MovieApp.Application.Models.Localization;

public sealed record PersonDetailLocalizationData(
    string? Biography,
    IReadOnlyList<PersonFilmographyLocalizationItem> Filmography);

public sealed record PersonFilmographyLocalizationItem(
    string MediaType,
    int TmdbId,
    string? Title,
    string? Character);
