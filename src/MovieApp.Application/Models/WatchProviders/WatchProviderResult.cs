namespace MovieApp.Application.Models.WatchProviders;

public sealed record WatchProviderResult(
    int ProviderId,
    string Name,
    string? LogoPath,
    int DisplayPriority,
    IReadOnlyList<WatchProviderAvailabilityType> AvailabilityTypes,
    string? Link);
