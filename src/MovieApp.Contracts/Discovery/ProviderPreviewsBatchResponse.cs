namespace MovieApp.Contracts.Discovery;

public sealed record ProviderPreviewsBatchResponse(
    IReadOnlyList<ProviderPreviewItemResponse> Items);
