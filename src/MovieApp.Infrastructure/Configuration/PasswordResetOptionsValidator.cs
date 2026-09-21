using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Options;
using MovieApp.Application.Configuration;
using MovieApp.Application.Services.Identity;

namespace MovieApp.Infrastructure.Configuration;

public sealed class PasswordResetOptionsValidator(
    IHostEnvironment hostEnvironment) : IValidateOptions<PasswordResetOptions>
{
    public ValidateOptionsResult Validate(string? name, PasswordResetOptions options)
    {
        if (hostEnvironment.IsDevelopment() || hostEnvironment.IsEnvironment("Testing"))
        {
            return ValidateOptionsResult.Success;
        }

        if (!string.Equals(options.EmailProvider, "Resend", StringComparison.OrdinalIgnoreCase))
        {
            return ValidateOptionsResult.Fail(
                "Production requires Authentication:PasswordReset:EmailProvider to be set to 'Resend'. " +
                "Password reset must not run with a non-deliverable email sender.");
        }

        if (string.IsNullOrWhiteSpace(options.BaseUrl))
        {
            return ValidateOptionsResult.Fail(
                "Authentication:PasswordReset:BaseUrl is required in production.");
        }

        if (!EmailAuthActionUrlBuilder.IsProductionSafeAbsoluteUrl(options.BaseUrl))
        {
            return ValidateOptionsResult.Fail(
                "Authentication:PasswordReset:BaseUrl must be an absolute HTTPS URL in production " +
                "(for example https://moviecaveapp.com/auth/reset-password). " +
                "Custom URL schemes such as movieapp:// are not reliably clickable in email clients.");
        }

        return ValidateOptionsResult.Success;
    }
}
