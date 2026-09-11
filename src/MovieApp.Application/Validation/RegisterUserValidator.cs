using System.Net.Mail;
using MovieApp.Application.Models.Identity;

namespace MovieApp.Application.Validation;

public static class RegisterUserValidator
{
    public static SearchQueryValidationResult Validate(RegisterUserRequest request)
    {
        var emailValidation = ProfileValidator.ValidateEmail(request.Email);
        if (!emailValidation.IsValid)
        {
            return emailValidation;
        }

        var passwordValidation = PasswordPolicyValidator.Validate(request.Password);
        if (!passwordValidation.IsValid)
        {
            return passwordValidation;
        }

        return ProfileValidator.ValidateDisplayName(request.DisplayName);
    }
}
