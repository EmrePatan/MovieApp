using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Options;

namespace MovieApp.Infrastructure.Configuration;

public sealed class AppOptionsValidator(IHostEnvironment hostEnvironment) : IValidateOptions<AppOptions>
{
    public ValidateOptionsResult Validate(string? name, AppOptions options)
    {
        if (!hostEnvironment.IsProduction())
        {
            return ValidateOptionsResult.Success;
        }

        if (string.IsNullOrWhiteSpace(options.PublicBaseUrl))
        {
            return ValidateOptionsResult.Fail(
                "Production requires App:PublicBaseUrl to be configured with the public HTTPS API base URL.");
        }

        if (!Uri.TryCreate(options.PublicBaseUrl.Trim(), UriKind.Absolute, out var uri) ||
            !string.Equals(uri.Scheme, Uri.UriSchemeHttps, StringComparison.OrdinalIgnoreCase))
        {
            return ValidateOptionsResult.Fail(
                "App:PublicBaseUrl must be a valid absolute HTTPS URL (for example https://api.example.com).");
        }

        return ValidateOptionsResult.Success;
    }
}
