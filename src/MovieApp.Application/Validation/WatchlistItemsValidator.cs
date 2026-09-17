using MovieApp.Application.Models.Search;
using MovieApp.Application.Models.Watchlists;

namespace MovieApp.Application.Validation;

public static class WatchlistItemsValidator
{
    public static SearchQueryValidationResult ValidateMediaType(string? mediaType)
    {
        if (string.IsNullOrWhiteSpace(mediaType))
        {
            return SearchQueryValidationResult.Success();
        }

        return AdvancedSearchValidator.TryParseType(mediaType, out var parsed) && parsed != SearchContentType.Person
            ? SearchQueryValidationResult.Success()
            : SearchQueryValidationResult.Failure("Media type must be one of: all, movie, tv.");
    }

    public static SearchQueryValidationResult ValidateSort(string? sort)
    {
        if (string.IsNullOrWhiteSpace(sort))
        {
            return SearchQueryValidationResult.Success();
        }

        return TryParseSort(sort, out _)
            ? SearchQueryValidationResult.Success()
            : SearchQueryValidationResult.Failure(
                "Sort must be one of: recentlyAdded, titleAsc, ratingDesc.");
    }

    public static bool TryParseSort(string? sort, out WatchlistItemsSort parsedSort)
    {
        parsedSort = WatchlistItemsSort.RecentlyAdded;

        if (string.IsNullOrWhiteSpace(sort))
        {
            return true;
        }

        switch (sort.Trim().ToLowerInvariant())
        {
            case "recentlyadded":
                parsedSort = WatchlistItemsSort.RecentlyAdded;
                return true;
            case "titleasc":
                parsedSort = WatchlistItemsSort.TitleAsc;
                return true;
            case "ratingdesc":
                parsedSort = WatchlistItemsSort.RatingDesc;
                return true;
            default:
                return false;
        }
    }
}
