using MovieApp.Domain.Reviews;

namespace MovieApp.Application.Validation;

public static class ReviewContentValidator
{
    public static SearchQueryValidationResult Validate(string content)
    {
        if (string.IsNullOrWhiteSpace(content))
        {
            return SearchQueryValidationResult.Failure("Review content is required.");
        }

        if (content.Trim().Length > ReviewContentRules.MaxLength)
        {
            return SearchQueryValidationResult.Failure(
                $"Review content must not exceed {ReviewContentRules.MaxLength} characters.");
        }

        return SearchQueryValidationResult.Success();
    }
}
