using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Options;
using MovieApp.Application.Configuration;

namespace MovieApp.Infrastructure.Configuration;

public sealed class AvatarStorageOptionsValidator(IHostEnvironment hostEnvironment)
    : IValidateOptions<AvatarStorageOptions>
{
    public ValidateOptionsResult Validate(string? name, AvatarStorageOptions options)
    {
        if (!hostEnvironment.IsProduction())
        {
            return ValidateOptionsResult.Success;
        }

        if (!options.IsConfigured)
        {
            return ValidateOptionsResult.Fail(
                "Production requires AvatarStorage to be fully configured (Endpoint, BucketName, AccessKeyId, SecretAccessKey, PublicBaseUrl).");
        }

        if (!Uri.TryCreate(options.PublicBaseUrl.Trim(), UriKind.Absolute, out var publicUri) ||
            !string.Equals(publicUri.Scheme, Uri.UriSchemeHttps, StringComparison.OrdinalIgnoreCase))
        {
            return ValidateOptionsResult.Fail(
                "AvatarStorage:PublicBaseUrl must be a valid absolute HTTPS URL in production.");
        }

        if (!Uri.TryCreate(options.Endpoint.Trim(), UriKind.Absolute, out _))
        {
            return ValidateOptionsResult.Fail(
                "AvatarStorage:Endpoint must be a valid absolute URL in production.");
        }

        return ValidateOptionsResult.Success;
    }
}
