using MovieApp.Contracts.Search;

namespace MovieApp.Contracts.Discovery;

public sealed record ProviderPreviewItemResponse(
    int ProviderId,
    string Status,
    IReadOnlyList<SearchItemResponse> Items);
