namespace MovieApp.Application.Models.Search;

public sealed record ProviderPreviewGroupResult(
    int ProviderId,
    DiscoveryBatchItemStatus Status,
    IReadOnlyList<SearchItem> Items);
