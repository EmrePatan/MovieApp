namespace MovieApp.Application.Models.WatchProviders;

public sealed record WatchProvidersResult(
    string Region,
    IReadOnlyList<WatchProviderResult> Providers,
    string? AttributionLink);
