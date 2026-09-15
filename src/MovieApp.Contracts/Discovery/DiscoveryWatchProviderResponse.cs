namespace MovieApp.Contracts.Discovery;

public sealed record DiscoveryWatchProviderResponse(
    int ProviderId,
    string Name,
    string? LogoPath,
    int DisplayPriority);

public sealed record DiscoveryWatchProvidersResponse(
    string WatchRegion,
    string MediaType,
    IReadOnlyList<DiscoveryWatchProviderResponse> Providers);
