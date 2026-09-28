using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Options;
using MovieApp.Application.Configuration;

namespace MovieApp.Infrastructure.Configuration;

public sealed class CatalogShareOptionsValidator(IHostEnvironment hostEnvironment) : IValidateOptions<CatalogShareOptions>
{
    public ValidateOptionsResult Validate(string? name, CatalogShareOptions options)
    {
        if (!hostEnvironment.IsProduction())
        {
            return ValidateOptionsResult.Success;
        }

        if (string.IsNullOrWhiteSpace(options.PublicWebBaseUrl))
        {
            return ValidateOptionsResult.Fail(
                "Production requires CatalogShare:PublicWebBaseUrl (for example https://moviecaveapp.com).");
        }

        if (!Uri.TryCreate(options.PublicWebBaseUrl.Trim(), UriKind.Absolute, out var uri) ||
            !uri.Scheme.Equals("https", StringComparison.OrdinalIgnoreCase))
        {
            return ValidateOptionsResult.Fail(
                "CatalogShare:PublicWebBaseUrl must be a valid absolute HTTPS URL in production.");
        }

        return ValidateOptionsResult.Success;
    }
}
