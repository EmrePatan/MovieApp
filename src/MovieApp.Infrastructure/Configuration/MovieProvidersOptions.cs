using MovieApp.Infrastructure.Providers.Tmdb;

namespace MovieApp.Infrastructure.Configuration;

public sealed class MovieProvidersOptions
{
    public const string SectionName = "MovieProviders";

    public string Provider { get; set; } = MovieDataProviderNames.Fake;

    public TmdbOptions Tmdb { get; set; } = new();

    public MovieProviderOptions Tvdb { get; set; } = new();
}

public sealed class MovieProviderOptions
{
    public string BaseUrl { get; set; } = string.Empty;

    public string ApiKey { get; set; } = string.Empty;
}
