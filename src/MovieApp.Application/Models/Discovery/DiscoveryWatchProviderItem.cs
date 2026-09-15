namespace MovieApp.Application.Models.Discovery;

public sealed record DiscoveryWatchProviderItem(
    int ProviderId,
    string Name,
    string? LogoPath,
    int DisplayPriority);
