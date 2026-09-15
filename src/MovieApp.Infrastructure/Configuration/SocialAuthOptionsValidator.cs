using Microsoft.Extensions.Options;

namespace MovieApp.Infrastructure.Configuration;

public sealed class SocialAuthOptionsValidator : IValidateOptions<SocialAuthOptions>
{
    public ValidateOptionsResult Validate(string? name, SocialAuthOptions options)
    {
        if (options.Google.ClientIds.Any(string.IsNullOrWhiteSpace))
        {
            return ValidateOptionsResult.Fail("Authentication:Social:Google:ClientIds must not contain empty values.");
        }

        if (options.Apple.ClientIds.Any(string.IsNullOrWhiteSpace))
        {
            return ValidateOptionsResult.Fail("Authentication:Social:Apple:ClientIds must not contain empty values.");
        }

        return ValidateOptionsResult.Success;
    }
}
