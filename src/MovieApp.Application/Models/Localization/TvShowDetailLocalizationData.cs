namespace MovieApp.Application.Models.Localization;

public sealed record TvShowDetailLocalizationData(
    string? Title,
    string? Overview,
    string? Status,
    IReadOnlyDictionary<int, string?> SeasonNames);
