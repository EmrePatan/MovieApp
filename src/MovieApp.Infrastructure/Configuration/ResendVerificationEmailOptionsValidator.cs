using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Options;

namespace MovieApp.Infrastructure.Configuration;

public sealed class ResendVerificationEmailOptionsValidator(IHostEnvironment hostEnvironment)
    : IValidateOptions<ResendVerificationEmailOptions>
{
    public ValidateOptionsResult Validate(string? name, ResendVerificationEmailOptions options)
    {
        if (hostEnvironment.IsDevelopment() || hostEnvironment.IsEnvironment("Testing"))
        {
            return ValidateOptionsResult.Success;
        }

        if (!options.IsConfigured())
        {
            return ValidateOptionsResult.Fail(
                "Production requires Authentication:EmailVerification:Resend:ApiKey and FromAddress for verification email delivery.");
        }

        return ValidateOptionsResult.Success;
    }
}
