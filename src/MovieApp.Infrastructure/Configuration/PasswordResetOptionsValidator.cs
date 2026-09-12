using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Options;
using MovieApp.Application.Configuration;

namespace MovieApp.Infrastructure.Configuration;

public sealed class PasswordResetOptionsValidator(
    IHostEnvironment hostEnvironment,
    IOptions<SmtpEmailOptions> smtpEmailOptions) : IValidateOptions<PasswordResetOptions>
{
    public ValidateOptionsResult Validate(string? name, PasswordResetOptions options)
    {
        if (hostEnvironment.IsDevelopment() || hostEnvironment.IsEnvironment("Testing"))
        {
            return ValidateOptionsResult.Success;
        }

        if (!string.Equals(options.EmailProvider, "Smtp", StringComparison.OrdinalIgnoreCase))
        {
            return ValidateOptionsResult.Fail(
                "Production requires Authentication:PasswordReset:EmailProvider to be set to 'Smtp'. " +
                "Password reset must not run with a non-deliverable email sender.");
        }

        if (!smtpEmailOptions.Value.IsConfigured())
        {
            return ValidateOptionsResult.Fail(
                "Production SMTP email is not configured. Set Authentication:Email:Smtp:Host and FromAddress " +
                "(and credentials via secrets or environment variables).");
        }

        if (string.IsNullOrWhiteSpace(options.BaseUrl))
        {
            return ValidateOptionsResult.Fail(
                "Authentication:PasswordReset:BaseUrl is required in production.");
        }

        return ValidateOptionsResult.Success;
    }
}
