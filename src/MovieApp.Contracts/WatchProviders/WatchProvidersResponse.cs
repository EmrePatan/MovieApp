namespace MovieApp.Contracts.WatchProviders;

public sealed record WatchProvidersResponse(
    string Region,
    IReadOnlyList<WatchProviderResponse> Providers,
    string? AttributionLink);
