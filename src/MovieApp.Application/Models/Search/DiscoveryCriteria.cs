namespace MovieApp.Application.Models.Search;

public sealed record DiscoveryCriteria(
    SearchContentType Type,
    int Page,
    int PageSize);
