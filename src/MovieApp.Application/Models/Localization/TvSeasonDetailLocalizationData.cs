namespace MovieApp.Application.Models.Localization;

public sealed record TvSeasonDetailLocalizationData(
    string? Name,
    string? Overview,
    IReadOnlyList<TvSeasonEpisodeLocalizationItem> Episodes);

public sealed record TvSeasonEpisodeLocalizationItem(
    int EpisodeNumber,
    string? Name,
    string? Overview);
