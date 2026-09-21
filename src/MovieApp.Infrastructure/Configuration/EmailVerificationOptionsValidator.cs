using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Options;
using MovieApp.Application.Configuration;
using MovieApp.Application.Services.Identity;

namespace MovieApp.Infrastructure.Configuration;

public sealed class EmailVerificationOptionsValidator(IHostEnvironment hostEnvironment)
    : IValidateOptions<EmailVerificationOptions>
{
    public ValidateOptionsResult Validate(string? name, EmailVerificationOptions options)
    {
        if (hostEnvironment.IsDevelopment() || hostEnvironment.IsEnvironment("Testing"))
        {
            return ValidateOptionsResult.Success;
        }

        if (string.IsNullOrWhiteSpace(options.BaseUrl))
        {
            return ValidateOptionsResult.Fail(
                "Authentication:EmailVerification:BaseUrl is required in production.");
        }

        if (!EmailAuthActionUrlBuilder.IsProductionSafeAbsoluteUrl(options.BaseUrl))
        {
            return ValidateOptionsResult.Fail(
                "Authentication:EmailVerification:BaseUrl must be an absolute HTTPS URL in production " +
                "(for example https://moviecaveapp.com/auth/verify-email). " +
                "Custom URL schemes such as movieapp:// are not reliably clickable in email clients.");
        }

        return ValidateOptionsResult.Success;
    }
}
