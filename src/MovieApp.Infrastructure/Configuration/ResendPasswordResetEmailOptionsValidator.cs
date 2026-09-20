using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Options;
using MovieApp.Application.Configuration;

namespace MovieApp.Infrastructure.Configuration;

public sealed class ResendPasswordResetEmailOptionsValidator(
    IHostEnvironment hostEnvironment,
    IOptions<PasswordResetOptions> passwordResetOptions,
    IOptions<SharedResendEmailOptions> sharedResendOptions) : IValidateOptions<ResendPasswordResetEmailOptions>
{
    public ValidateOptionsResult Validate(string? name, ResendPasswordResetEmailOptions options)
    {
        if (hostEnvironment.IsDevelopment() || hostEnvironment.IsEnvironment("Testing"))
        {
            return ValidateOptionsResult.Success;
        }

        if (!string.Equals(
                passwordResetOptions.Value.EmailProvider,
                "Resend",
                StringComparison.OrdinalIgnoreCase))
        {
            return ValidateOptionsResult.Success;
        }

        var effective = ResendEmailDeliverySettingsResolver.Resolve(
            options.ApiKey,
            options.FromAddress,
            options.FromName,
            sharedResendOptions.Value);

        if (!effective.IsConfigured())
        {
            return ValidateOptionsResult.Fail(
                "Production password reset email via Resend is not configured. " +
                "Set Authentication:Email:Resend or Authentication:PasswordReset:Resend ApiKey and FromAddress.");
        }

        return ValidateOptionsResult.Success;
    }
}
