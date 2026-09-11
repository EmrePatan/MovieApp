using MovieApp.Domain.Ratings;

namespace MovieApp.Application.Validation;

public static class RatingScoreValidator
{
    public static SearchQueryValidationResult Validate(int score)
    {
        if (score < RatingScoreRules.MinScore || score > RatingScoreRules.MaxScore)
        {
            return SearchQueryValidationResult.Failure(
                $"Score must be between {RatingScoreRules.MinScore} and {RatingScoreRules.MaxScore}.");
        }

        return SearchQueryValidationResult.Success();
    }
}
