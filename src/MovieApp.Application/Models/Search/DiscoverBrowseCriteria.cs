namespace MovieApp.Application.Models.Search;

public sealed record DiscoverBrowseCriteria(
    DiscoverBrowseMode Mode,
    SearchContentType Type,
    IReadOnlyList<Guid> GenreIds,
    int? Year,
    decimal? MinRating,
    string? Language,
    DiscoverBrowseSort? Sort,
    int Page,
    int PageSize);
