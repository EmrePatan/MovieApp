using MovieApp.Application.Common;
using MovieApp.Application.Models.Common;
using MovieApp.Application.Models.Search;
using MovieApp.Application.Search;

namespace MovieApp.Application.Validation;

public static class AdvancedSearchValidator
{
    public const int MinimumQueryLength = 2;
    public const int MaximumQueryLength = 100;
    public const int MinimumYear = 1888;
    public const decimal MinimumRating = 0m;
    public const decimal MaximumRating = 10m;

    public static SearchQueryValidationResult ValidateQuery(string? query, bool required = false)
    {
        if (string.IsNullOrWhiteSpace(query))
        {
            return required
                ? SearchQueryValidationResult.Failure("Search query is required.")
                : SearchQueryValidationResult.Success();
        }

        var trimmedQuery = QueryNormalizer.CollapseWhitespace(query);

        if (trimmedQuery.Length < MinimumQueryLength)
        {
            return SearchQueryValidationResult.Failure(
                $"Search query must be at least {MinimumQueryLength} characters.");
        }

        if (trimmedQuery.Length > MaximumQueryLength)
        {
            return SearchQueryValidationResult.Failure(
                $"Search query must not exceed {MaximumQueryLength} characters.");
        }

        return SearchQueryValidationResult.Success();
    }

    public static SearchQueryValidationResult ValidatePagination(int page, int pageSize) =>
        SearchPaginationValidator.Validate(page, pageSize);

    public static SearchQueryValidationResult ValidateType(string? type)
    {
        if (string.IsNullOrWhiteSpace(type))
        {
            return SearchQueryValidationResult.Success();
        }

        return TryParseType(type, out _)
            ? SearchQueryValidationResult.Success()
            : SearchQueryValidationResult.Failure("Type must be one of: movie, tv, person, all.");
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
                "Sort must be one of: relevance, rating, rating_desc, rating_asc, date_desc, date_asc, title_asc, title_desc, popular.");
    }

    public static SearchQueryValidationResult ValidateYear(int? year)
    {
        if (year is null)
        {
            return SearchQueryValidationResult.Success();
        }

        var maximumYear = DateTime.UtcNow.Year + 1;
        if (year < MinimumYear || year > maximumYear)
        {
            return SearchQueryValidationResult.Failure(
                $"Year must be between {MinimumYear} and {maximumYear}.");
        }

        return SearchQueryValidationResult.Success();
    }

    public static SearchQueryValidationResult ValidateRatings(decimal? minRating, decimal? maxRating)
    {
        if (minRating is < MinimumRating or > MaximumRating)
        {
            return SearchQueryValidationResult.Failure(
                $"Min rating must be between {MinimumRating} and {MaximumRating}.");
        }

        if (maxRating is < MinimumRating or > MaximumRating)
        {
            return SearchQueryValidationResult.Failure(
                $"Max rating must be between {MinimumRating} and {MaximumRating}.");
        }

        if (minRating is not null && maxRating is not null && minRating > maxRating)
        {
            return SearchQueryValidationResult.Failure("Min rating must not exceed max rating.");
        }

        return SearchQueryValidationResult.Success();
    }

    public static SearchQueryValidationResult Validate(SearchCriteria criteria, bool queryRequired = false)
    {
        var queryValidation = ValidateQuery(criteria.Query, queryRequired);
        if (!queryValidation.IsValid)
        {
            return queryValidation;
        }

        if (!string.IsNullOrWhiteSpace(criteria.Cursor))
        {
            var normalizedQuery = string.IsNullOrWhiteSpace(criteria.Query)
                ? null
                : QueryNormalizer.Normalize(criteria.Query);
            if (!SearchKeysetCursor.TryDecode(criteria.Cursor, criteria, normalizedQuery, out _, out var cursorError))
            {
                return SearchQueryValidationResult.Failure(cursorError!);
            }

            if (criteria.PageSize < 1 || criteria.PageSize > SearchPaginationDefaults.MaxPageSize)
            {
                return SearchQueryValidationResult.Failure(
                    $"Page size must not exceed {SearchPaginationDefaults.MaxPageSize}.");
            }
        }
        else
        {
            var paginationValidation = ValidatePagination(criteria.Page, criteria.PageSize);
            if (!paginationValidation.IsValid)
            {
                return paginationValidation;
            }
        }

        var yearValidation = ValidateYear(criteria.Year);
        if (!yearValidation.IsValid)
        {
            return yearValidation;
        }

        return ValidateRatings(criteria.MinRating, criteria.MaxRating);
    }

    public static bool TryParseType(string? type, out SearchContentType contentType)
    {
        contentType = SearchContentType.All;

        if (string.IsNullOrWhiteSpace(type))
        {
            return true;
        }

        switch (type.Trim().ToLowerInvariant())
        {
            case "movie":
                contentType = SearchContentType.Movie;
                return true;
            case "tv":
                contentType = SearchContentType.Tv;
                return true;
            case "person":
                contentType = SearchContentType.Person;
                return true;
            case "all":
                contentType = SearchContentType.All;
                return true;
            default:
                return false;
        }
    }

    public static bool TryParseSort(string? sort, out SearchSortOption sortOption)
    {
        sortOption = SearchSortOption.Relevance;

        if (string.IsNullOrWhiteSpace(sort))
        {
            return true;
        }

        switch (sort.Trim().ToLowerInvariant())
        {
            case "relevance":
                sortOption = SearchSortOption.Relevance;
                return true;
            case "rating":
            case "rating_desc":
                sortOption = SearchSortOption.RatingDesc;
                return true;
            case "rating_asc":
                sortOption = SearchSortOption.RatingAsc;
                return true;
            case "date_desc":
                sortOption = SearchSortOption.DateDesc;
                return true;
            case "date_asc":
                sortOption = SearchSortOption.DateAsc;
                return true;
            case "title_asc":
                sortOption = SearchSortOption.TitleAsc;
                return true;
            case "title_desc":
                sortOption = SearchSortOption.TitleDesc;
                return true;
            case "popular":
                sortOption = SearchSortOption.Popular;
                return true;
            default:
                return false;
        }
    }
}
