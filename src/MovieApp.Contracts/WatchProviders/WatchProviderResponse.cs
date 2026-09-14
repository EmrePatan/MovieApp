namespace MovieApp.Contracts.WatchProviders;

public sealed record WatchProviderResponse(
    int ProviderId,
    string Name,
    string? LogoPath,
    int DisplayPriority,
    IReadOnlyList<string> AvailabilityTypes,
    string? Link);
