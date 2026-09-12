using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Options;

namespace MovieApp.Infrastructure.Configuration;

public sealed class JwtOptionsValidator(IHostEnvironment hostEnvironment) : IValidateOptions<JwtOptions>
{
    public ValidateOptionsResult Validate(string? name, JwtOptions options)
    {
        if (!hostEnvironment.IsProduction())
        {
            return ValidateOptionsResult.Success;
        }

        if (!JwtSigningKeyProductionRules.IsAcceptableProductionSigningKey(options.SigningKey))
        {
            return ValidateOptionsResult.Fail(
                "Production requires Authentication:Jwt:SigningKey to be configured with at least 32 secure random bytes. " +
                "Known development or test signing keys are not permitted.");
        }

        return ValidateOptionsResult.Success;
    }
}
