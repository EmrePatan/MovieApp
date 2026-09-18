using MovieApp.Application.Models.Reviews;
using MovieApp.Application.Models.Search;

namespace MovieApp.Application.Validation;

public static class ReviewListValidator
{
    public static SearchQueryValidationResult ValidateSort(string? sort)
    {
        if (string.IsNullOrWhiteSpace(sort))
        {
            return SearchQueryValidationResult.Success();
        }

        return TryParseSort(sort, out _)
            ? SearchQueryValidationResult.Success()
            : SearchQueryValidationResult.Failure(
                "Sort must be one of: newest, oldest, ratingDesc, ratingAsc.");
    }

    public static bool TryParseSort(string? sort, out ReviewListSort parsedSort)
    {
        parsedSort = ReviewListSort.Newest;

        if (string.IsNullOrWhiteSpace(sort))
        {
            return true;
        }

        switch (sort.Trim().ToLowerInvariant())
        {
            case "newest":
                parsedSort = ReviewListSort.Newest;
                return true;
            case "oldest":
                parsedSort = ReviewListSort.Oldest;
                return true;
            case "ratingdesc":
                parsedSort = ReviewListSort.RatingDesc;
                return true;
            case "ratingasc":
                parsedSort = ReviewListSort.RatingAsc;
                return true;
            default:
                return false;
        }
    }
}
