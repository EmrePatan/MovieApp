using MovieApp.Application.Models.Identity;

namespace MovieApp.Application.Validation;

public static class LoginUserValidator
{
    public static SearchQueryValidationResult Validate(LoginUserRequest request)
    {
        if (string.IsNullOrWhiteSpace(request.Email))
        {
            return SearchQueryValidationResult.Failure("Email is required.");
        }

        if (string.IsNullOrWhiteSpace(request.Password))
        {
            return SearchQueryValidationResult.Failure("Password is required.");
        }

        if (request.Password.Length > PasswordPolicyValidator.MaxLength)
        {
            return SearchQueryValidationResult.Failure(
                $"Password must not exceed {PasswordPolicyValidator.MaxLength} characters.");
        }

        return SearchQueryValidationResult.Success();
    }
}
