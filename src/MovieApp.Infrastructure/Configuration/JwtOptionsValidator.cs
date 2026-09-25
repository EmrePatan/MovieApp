using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Options;

namespace MovieApp.Infrastructure.Configuration;

public sealed class JwtOptionsValidator(IHostEnvironment hostEnvironment) : IValidateOptions<JwtOptions>
{
    public ValidateOptionsResult Validate(string? name, JwtOptions options)
    {
        if (hostEnvironment.IsProduction())
        {
            if (!JwtSigningKeyProductionRules.IsAcceptableProductionSigningKey(options.SigningKey))
            {
                return ValidateOptionsResult.Fail(
                    "Production requires Authentication:Jwt:SigningKey to be configured with at least 32 secure random bytes. " +
                    "Known development or test signing keys are not permitted.");
            }

            return ValidateOptionsResult.Success;
        }

        if (hostEnvironment.IsDevelopment() && !HasDevelopmentSigningKey(options.SigningKey))
        {
            return ValidateOptionsResult.Fail(
                "Development requires Authentication:Jwt:SigningKey of at least 32 characters. " +
                "Set it with user secrets or the Authentication__Jwt__SigningKey environment variable. " +
                "See .env.example. The API will not start while the signing key is missing or too short, " +
                "because login cannot create access tokens.");
        }

        return ValidateOptionsResult.Success;
    }

    private static bool HasDevelopmentSigningKey(string signingKey) =>
        !string.IsNullOrWhiteSpace(signingKey) &&
        signingKey.Length >= JwtSigningKeyProductionRules.MinimumKeyLength;
}
