using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Options;

namespace MovieApp.Infrastructure.Configuration;

public sealed class SocialAuthOptionsValidator(IHostEnvironment hostEnvironment)
    : IValidateOptions<SocialAuthOptions>
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

        if (hostEnvironment.IsProduction() && !HasAnyClientId(options))
        {
            return ValidateOptionsResult.Fail(
                "Production requires at least one non-empty Authentication:Social:Google:ClientIds or Authentication:Social:Apple:ClientIds value.");
        }

        return ValidateOptionsResult.Success;
    }

    private static bool HasAnyClientId(SocialAuthOptions options) =>
        options.Google.ClientIds.Any(static id => !string.IsNullOrWhiteSpace(id))
        || options.Apple.ClientIds.Any(static id => !string.IsNullOrWhiteSpace(id));
}
