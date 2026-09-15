using MovieApp.Application.Models.Common;

namespace MovieApp.Application.Models.Search;

public sealed record ExplorePreviewCriteria(int SectionSize)
{
    public static ExplorePreviewCriteria Default => new(SearchPaginationDefaults.DefaultPageSize);
}
