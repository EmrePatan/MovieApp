using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Options;

namespace MovieApp.Infrastructure.Configuration;

public sealed class ResendVerificationEmailOptionsValidator(
    IHostEnvironment hostEnvironment,
    IOptions<SharedResendEmailOptions> sharedResendOptions) : IValidateOptions<ResendVerificationEmailOptions>
{
    public ValidateOptionsResult Validate(string? name, ResendVerificationEmailOptions options)
    {
        if (hostEnvironment.IsDevelopment() || hostEnvironment.IsEnvironment("Testing"))
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
                "Production requires Resend verification email configuration. " +
                "Set Authentication:Email:Resend or Authentication:EmailVerification:Resend ApiKey and FromAddress.");
        }

        if (ResendProductionFromAddressRules.IsOnboardingSender(effective.FromAddress))
        {
            return ValidateOptionsResult.Fail(
                "Production verification email must use a verified custom-domain sender address. " +
                "Resend onboarding addresses such as onboarding@resend.dev are not allowed.");
        }

        return ValidateOptionsResult.Success;
    }
}
