namespace MovieApp.Application.Models.Search;

public sealed record ProviderPreviewsBatchCriteria(
    IReadOnlyList<int> ProviderIds,
    SearchContentType MediaType,
    string WatchRegion,
    int PageSize);
