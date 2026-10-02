using MovieApp.Application.Models.Search;

namespace MovieApp.Application.Models.Library;

public sealed record LibrarySearchCriteria(
    string Query,
    SearchContentType MediaType,
    int Page,
    int PageSize);
