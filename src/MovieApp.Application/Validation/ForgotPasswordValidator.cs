using MovieApp.Application.Common;
using MovieApp.Application.Models.Identity;

namespace MovieApp.Application.Validation;

public static class ForgotPasswordValidator
{
    public static SearchQueryValidationResult Validate(string? email)
    {
        if (string.IsNullOrWhiteSpace(email))
        {
            return SearchQueryValidationResult.Failure("Email is required.");
        }

        var trimmed = email.Trim();
        if (trimmed.Length > 320)
        {
            return SearchQueryValidationResult.Failure("Email must not exceed 320 characters.");
        }

        return ProfileValidator.ValidateEmail(trimmed);
    }
}
