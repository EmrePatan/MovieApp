using MovieApp.Application.Models.Search;

namespace MovieApp.Application.Models.Library;

public sealed record LibraryCriteria(
    LibraryCategory Category,
    SearchContentType MediaType,
    int Page,
    int PageSize,
    string? Cursor = null);
