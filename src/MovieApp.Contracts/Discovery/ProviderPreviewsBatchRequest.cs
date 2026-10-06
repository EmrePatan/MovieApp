namespace MovieApp.Contracts.Discovery;

public sealed record ProviderPreviewsBatchRequest(
    IReadOnlyList<int>? ProviderIds,
    string MediaType,
    string WatchRegion,
    int? PageSize = null);
