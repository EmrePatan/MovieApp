namespace MovieApp.Infrastructure.Providers.Tmdb;

public sealed class TmdbOptions
{
    public string BaseUrl { get; set; } = "https://api.themoviedb.org/3/";

    public string ApiKey { get; set; } = string.Empty;

    public string ReadAccessToken { get; set; } = string.Empty;

    public bool IsConfigured() =>
        !string.IsNullOrWhiteSpace(ReadAccessToken) || !string.IsNullOrWhiteSpace(ApiKey);

    public bool UsesBearerAuthentication() =>
        !string.IsNullOrWhiteSpace(ReadAccessToken);
}
