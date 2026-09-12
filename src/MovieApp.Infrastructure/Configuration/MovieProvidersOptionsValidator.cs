using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Options;

namespace MovieApp.Infrastructure.Configuration;

public sealed class MovieProvidersOptionsValidator(IHostEnvironment hostEnvironment)
    : IValidateOptions<MovieProvidersOptions>
{
    public ValidateOptionsResult Validate(string? name, MovieProvidersOptions options)
    {
        if (!hostEnvironment.IsProduction())
        {
            return ValidateOptionsResult.Success;
        }

        var provider = options.Provider?.Trim() ?? string.Empty;

        if (string.IsNullOrWhiteSpace(provider) ||
            string.Equals(provider, MovieDataProviderNames.Fake, StringComparison.OrdinalIgnoreCase))
        {
            return ValidateOptionsResult.Fail(
                "Production requires MovieProviders:Provider to be set to 'Tmdb'. The Fake provider is not permitted in Production.");
        }

        if (!string.Equals(provider, MovieDataProviderNames.Tmdb, StringComparison.OrdinalIgnoreCase))
        {
            return ValidateOptionsResult.Fail(
                $"Production only supports MovieProviders:Provider='Tmdb'. The configured provider '{provider}' is not supported.");
        }

        if (!options.Tmdb.IsConfigured())
        {
            return ValidateOptionsResult.Fail(
                "Production TMDB provider requires MovieProviders:Tmdb:ReadAccessToken or MovieProviders:Tmdb:ApiKey to be configured.");
        }

        return ValidateOptionsResult.Success;
    }
}
