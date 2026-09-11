using MovieApp.Application.Models.Search;

namespace MovieApp.Application.Validation;

public static class HomeValidator
{
    public static SearchQueryValidationResult ValidateSectionSize(int sectionSize, int minimum, int maximum)
    {
        if (sectionSize < minimum)
        {
            return SearchQueryValidationResult.Failure($"Section size must be at least {minimum}.");
        }

        if (sectionSize > maximum)
        {
            return SearchQueryValidationResult.Failure($"Section size must not exceed {maximum}.");
        }

        return SearchQueryValidationResult.Success();
    }

    public static SearchQueryValidationResult ValidateType(string? type) =>
        RecommendationValidator.ValidateType(type);

    public static bool TryParseType(string? type, out SearchContentType contentType)
    {
        contentType = SearchContentType.All;

        if (!RecommendationValidator.TryParseType(type, out var recommendationType))
        {
            return false;
        }

        contentType = recommendationType switch
        {
            Models.Recommendations.RecommendationContentType.Movie => SearchContentType.Movie,
            Models.Recommendations.RecommendationContentType.Tv => SearchContentType.Tv,
            _ => SearchContentType.All
        };

        return true;
    }
}
