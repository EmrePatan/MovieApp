using MovieApp.Application.Models.Common;
using MovieApp.Application.Models.Discovery;

namespace MovieApp.Application.Validation;

public static class OnTvThisWeekValidator
{
    public static SearchQueryValidationResult Validate(OnTvThisWeekCriteria criteria)
    {
        if (criteria.Page < SearchPaginationDefaults.MinPage)
        {
            return SearchQueryValidationResult.Failure("Page must be at least 1.");
        }

        if (criteria.PageSize < 1 || criteria.PageSize > SearchPaginationDefaults.MaxPageSize)
        {
            return SearchQueryValidationResult.Failure(
                $"Page size must be between 1 and {SearchPaginationDefaults.MaxPageSize}.");
        }

        return SearchQueryValidationResult.Success();
    }
}
