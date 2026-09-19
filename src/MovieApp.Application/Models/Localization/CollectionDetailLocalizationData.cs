namespace MovieApp.Application.Models.Localization;

public sealed record CollectionDetailLocalizationData(
    string? Name,
    string? Overview,
    IReadOnlyDictionary<int, CollectionPartLocalizationEntry> Parts);

public sealed record CollectionPartLocalizationEntry(
    string? Title,
    string? Overview);
