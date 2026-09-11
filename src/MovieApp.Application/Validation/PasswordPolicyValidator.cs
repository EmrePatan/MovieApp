namespace MovieApp.Application.Validation;

public static class PasswordPolicyValidator
{
    public const int MinLength = 8;

    public const int MaxLength = 128;

    public static SearchQueryValidationResult Validate(string? password)
    {
        if (string.IsNullOrWhiteSpace(password))
        {
            return SearchQueryValidationResult.Failure("Password is required.");
        }

        if (password.Length < MinLength)
        {
            return SearchQueryValidationResult.Failure(
                $"Password must be at least {MinLength} characters.");
        }

        if (password.Length > MaxLength)
        {
            return SearchQueryValidationResult.Failure(
                $"Password must not exceed {MaxLength} characters.");
        }

        return SearchQueryValidationResult.Success();
    }
}
