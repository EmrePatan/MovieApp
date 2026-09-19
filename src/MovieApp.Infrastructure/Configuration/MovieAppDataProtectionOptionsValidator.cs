using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Options;

namespace MovieApp.Infrastructure.Configuration;

public sealed class MovieAppDataProtectionOptionsValidator(
    IHostEnvironment hostEnvironment,
    IOptions<PostgreSqlOptions> postgreSqlOptions) : IValidateOptions<MovieAppDataProtectionOptions>
{
    public ValidateOptionsResult Validate(string? name, MovieAppDataProtectionOptions options)
    {
        if (!hostEnvironment.IsProduction())
        {
            return ValidateOptionsResult.Success;
        }

        if (!postgreSqlOptions.Value.IsConfigured())
        {
            return ValidateOptionsResult.Fail(
                "Production requires PostgreSql configuration so Data Protection keys can be shared across instances.");
        }

        if (string.IsNullOrWhiteSpace(options.KeyEncryptionKeyBase64))
        {
            return ValidateOptionsResult.Fail(
                "Production requires DataProtection:KeyEncryptionKeyBase64 so key-ring material stored in PostgreSQL remains encrypted at rest.");
        }

        try
        {
            _ = options.TryGetKeyEncryptionKey();
        }
        catch (InvalidOperationException exception)
        {
            return ValidateOptionsResult.Fail(exception.Message);
        }

        return ValidateOptionsResult.Success;
    }
}
