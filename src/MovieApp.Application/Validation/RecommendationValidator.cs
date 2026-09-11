using MovieApp.Application.Models.Recommendations;

namespace MovieApp.Application.Validation;

public static class RecommendationValidator
{
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
            : SearchQueryValidationResult.Failure("Type must be one of: movie, tv, all.");
    }

    public static bool TryParseType(string? type, out RecommendationContentType contentType)
    {
        contentType = RecommendationContentType.All;

        if (string.IsNullOrWhiteSpace(type))
        {
            return true;
        }

        switch (type.Trim().ToLowerInvariant())
        {
            case "movie":
                contentType = RecommendationContentType.Movie;
                return true;
            case "tv":
                contentType = RecommendationContentType.Tv;
                return true;
            case "all":
                contentType = RecommendationContentType.All;
                return true;
            default:
                return false;
        }
    }
}
