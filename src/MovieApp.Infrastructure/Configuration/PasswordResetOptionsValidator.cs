using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Options;
using MovieApp.Application.Configuration;

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

        return ValidateOptionsResult.Success;
    }
}
