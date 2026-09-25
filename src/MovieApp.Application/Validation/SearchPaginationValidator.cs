using MovieApp.Application.Models.Movies;

namespace MovieApp.Application.Validation;

public static class SearchPaginationValidator
{
    public static SearchQueryValidationResult Validate(int page, int pageSize)
    {
        if (page < MovieSearchPagination.MinPage)
        {
            return SearchQueryValidationResult.Failure("Page must be at least 1.");
        }

        if (pageSize < 1)
        {
            return SearchQueryValidationResult.Failure("Page size must be at least 1.");
        }

        if (pageSize > MovieSearchPagination.MaxPageSize)
        {
            return SearchQueryValidationResult.Failure(
                $"Page size must not exceed {MovieSearchPagination.MaxPageSize}.");
        }

        // Skip/Take take a 32-bit count. (page - 1) * pageSize overflows to a negative
        // offset for large pages and EF throws ArgumentOutOfRangeException (HTTP 500).
        if ((long)(page - 1) * pageSize > int.MaxValue)
        {
            return SearchQueryValidationResult.Failure(
                "Page is too large for the requested page size.");
        }

        return SearchQueryValidationResult.Success();
    }
}
