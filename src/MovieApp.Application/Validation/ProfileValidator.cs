using System.Net.Mail;

namespace MovieApp.Application.Validation;

public static class ProfileValidator
{
    public const int DisplayNameMaxLength = 100;

    public const int EmailMaxLength = 320;

    public static SearchQueryValidationResult ValidateDisplayName(string? displayName)
    {
        if (string.IsNullOrWhiteSpace(displayName))
        {
            return SearchQueryValidationResult.Failure("Display name is required.");
        }

        if (displayName.Trim().Length > DisplayNameMaxLength)
        {
            return SearchQueryValidationResult.Failure(
                $"Display name must not exceed {DisplayNameMaxLength} characters.");
        }

        return SearchQueryValidationResult.Success();
    }

    public static SearchQueryValidationResult ValidateEmail(string? email)
    {
        if (string.IsNullOrWhiteSpace(email))
        {
            return SearchQueryValidationResult.Failure("Email is required.");
        }

        var trimmedEmail = email.Trim();
        if (trimmedEmail.Length > EmailMaxLength)
        {
            return SearchQueryValidationResult.Failure(
                $"Email must not exceed {EmailMaxLength} characters.");
        }

        if (!IsValidEmail(trimmedEmail))
        {
            return SearchQueryValidationResult.Failure("Email format is invalid.");
        }

        return SearchQueryValidationResult.Success();
    }

    public static SearchQueryValidationResult ValidateCurrentPassword(string? currentPassword)
    {
        if (string.IsNullOrWhiteSpace(currentPassword))
        {
            return SearchQueryValidationResult.Failure("Current password is required.");
        }

        return SearchQueryValidationResult.Success();
    }

    private static bool IsValidEmail(string email)
    {
        try
        {
            _ = new MailAddress(email);
            return true;
        }
        catch (FormatException)
        {
            return false;
        }
    }
}
