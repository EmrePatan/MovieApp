using MovieApp.Application.Models.Common;

namespace MovieApp.Application.Models.Discovery;

public sealed record NowInTheatersCriteria(
    string ReleaseRegion,
    int Page,
    int PageSize)
{
    public static NowInTheatersCriteria Default(string releaseRegion) =>
        new(
            releaseRegion,
            SearchPaginationDefaults.DefaultPage,
            SearchPaginationDefaults.DefaultPageSize);
}
