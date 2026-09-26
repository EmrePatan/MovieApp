using MovieApp.Application.Library;
using MovieApp.Application.Models.Library;
using MovieApp.Application.Models.Search;

namespace MovieApp.Application.Validation;

public static class LibraryValidator
{
    public const int DefaultPageSize = 24;
    public const int MaxPageSize = 100;

    public static SearchQueryValidationResult Validate(LibraryCriteria criteria)
    {
        if (!string.IsNullOrWhiteSpace(criteria.Cursor))
        {
            if (criteria.PageSize < 1 || criteria.PageSize > MaxPageSize)
            {
                return SearchQueryValidationResult.Failure($"Page size cannot exceed {MaxPageSize}.");
            }

            return SearchQueryValidationResult.Success();
        }

        var pagination = SearchPaginationValidator.Validate(criteria.Page, criteria.PageSize);
        if (!pagination.IsValid)
        {
            return pagination;
        }

        if (criteria.PageSize > MaxPageSize)
        {
            return SearchQueryValidationResult.Failure($"Page size cannot exceed {MaxPageSize}.");
        }

        return SearchQueryValidationResult.Success();
    }

    public static SearchQueryValidationResult ValidateCursor(
        LibraryCriteria criteria,
        Guid userId)
    {
        if (string.IsNullOrWhiteSpace(criteria.Cursor))
        {
            return SearchQueryValidationResult.Success();
        }

        return LibraryKeysetCursor.TryDecode(criteria.Cursor, userId, criteria, out _, out var error)
            ? SearchQueryValidationResult.Success()
            : SearchQueryValidationResult.Failure(error!);
    }

    public static SearchQueryValidationResult ValidateCategory(string? category)
    {
        if (string.IsNullOrWhiteSpace(category))
        {
            return SearchQueryValidationResult.Success();
        }

        return TryParseCategory(category, out _)
            ? SearchQueryValidationResult.Success()
            : SearchQueryValidationResult.Failure("Category must be one of: watching, watched, liked, watchlist.");
    }

    public static SearchQueryValidationResult ValidateMediaType(string? mediaType)
    {
        if (string.IsNullOrWhiteSpace(mediaType))
        {
            return SearchQueryValidationResult.Success();
        }

        return AdvancedSearchValidator.TryParseType(mediaType, out _)
            ? SearchQueryValidationResult.Success()
            : SearchQueryValidationResult.Failure("Media type must be one of: all, movie, tv.");
    }

    public static bool TryParseCategory(string? category, out LibraryCategory libraryCategory)
    {
        libraryCategory = LibraryCategory.Watching;

        if (string.IsNullOrWhiteSpace(category))
        {
            return true;
        }

        switch (category.Trim().ToLowerInvariant())
        {
            case "watching":
                libraryCategory = LibraryCategory.Watching;
                return true;
            case "watched":
                libraryCategory = LibraryCategory.Watched;
                return true;
            case "liked":
                libraryCategory = LibraryCategory.Liked;
                return true;
            case "watchlist":
                libraryCategory = LibraryCategory.Watchlist;
                return true;
            default:
                return false;
        }
    }
}
