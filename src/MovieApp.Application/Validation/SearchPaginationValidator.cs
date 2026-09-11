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

        return SearchQueryValidationResult.Success();
    }
}
