using MovieApp.Application.Models.Common;

namespace MovieApp.Application.Models.Discovery;

public sealed record OnTvThisWeekCriteria(int Page, int PageSize)
{
    public static OnTvThisWeekCriteria Default =>
        new(SearchPaginationDefaults.DefaultPage, SearchPaginationDefaults.DefaultPageSize);
}
