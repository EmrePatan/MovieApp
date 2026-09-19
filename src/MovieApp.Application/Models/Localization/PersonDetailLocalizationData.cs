namespace MovieApp.Application.Models.Localization;

public sealed record PersonDetailLocalizationData(
    string? Biography,
    IReadOnlyDictionary<PersonFilmographyLocalizationKey, PersonFilmographyLocalizationEntry> Filmography);

public sealed record PersonFilmographyLocalizationKey(string MediaType, int TmdbId);

public sealed record PersonFilmographyLocalizationEntry(
    string? Title,
    string? Character);
