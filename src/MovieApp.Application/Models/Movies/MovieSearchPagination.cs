using MovieApp.Application.Models.Common;

namespace MovieApp.Application.Models.Movies;

public static class MovieSearchPagination
{
    public const int DefaultPage = SearchPaginationDefaults.DefaultPage;

    public const int DefaultPageSize = SearchPaginationDefaults.DefaultPageSize;

    public const int MinPage = SearchPaginationDefaults.MinPage;

    public const int MaxPageSize = SearchPaginationDefaults.MaxPageSize;
}
