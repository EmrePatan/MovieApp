namespace MovieApp.Application.Models.Search;

public sealed record SearchCriteria(
    string? Query,
    SearchContentType Type,
    Guid? GenreId,
    int? Year,
    decimal? MinRating,
    decimal? MaxRating,
    SearchSortOption Sort,
    int Page,
    int PageSize);
