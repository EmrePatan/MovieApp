using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Options;

namespace MovieApp.Infrastructure.Configuration;

public sealed class MovieAppDataProtectionOptionsValidator(
    IHostEnvironment hostEnvironment,
    IOptions<RedisOptions> redisOptions) : IValidateOptions<MovieAppDataProtectionOptions>
{
    public ValidateOptionsResult Validate(string? name, MovieAppDataProtectionOptions options)
    {
        if (!hostEnvironment.IsProduction())
        {
            return ValidateOptionsResult.Success;
        }

        if (!redisOptions.Value.IsConfigured())
        {
            return ValidateOptionsResult.Fail(
                "Production requires Redis:ConnectionString so Data Protection keys can be shared across instances.");
        }

        if (string.IsNullOrWhiteSpace(options.CertificatePath))
        {
            return ValidateOptionsResult.Fail(
                "Production requires DataProtection:CertificatePath so key-ring material stored in Redis remains encrypted at rest.");
        }

        return ValidateOptionsResult.Success;
    }
}
