using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Options;

namespace MovieApp.Infrastructure.Configuration;

public sealed class CorsOptionsValidator(IHostEnvironment hostEnvironment) : IValidateOptions<CorsOptions>
{
    public ValidateOptionsResult Validate(string? name, CorsOptions options)
    {
        if (!hostEnvironment.IsProduction())
        {
            return ValidateOptionsResult.Success;
        }

        if (!options.Enabled)
        {
            // Native mobile clients do not use browser CORS; disabled CORS is valid for V1 production.
            return ValidateOptionsResult.Success;
        }

        var validOrigins = options.GetValidOrigins(requireHttps: true);
        if (validOrigins.Count == 0)
        {
            return ValidateOptionsResult.Fail(
                "Cors:Enabled is true in Production but no valid HTTPS allowed origins are configured. " +
                "Set Cors:AllowedOrigins (for example Cors__AllowedOrigins__0=https://app.example.com) " +
                "or disable Cors:Enabled when only native mobile clients are used.");
        }

        return ValidateOptionsResult.Success;
    }
}
