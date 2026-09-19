namespace MovieApp.Infrastructure.Providers.Tmdb;

public sealed class TmdbOptions
{
    public string BaseUrl { get; set; } = "https://api.themoviedb.org/3/";

    public string ApiKey { get; set; } = string.Empty;

    public string ReadAccessToken { get; set; } = string.Empty;

    /// <summary>
    /// TMDB language parameter used for all catalog persistence and upsert ingest paths.
    /// </summary>
    public string CanonicalLanguage { get; set; } = "en-US";

    public bool IsConfigured() =>
        !string.IsNullOrWhiteSpace(ReadAccessToken) || !string.IsNullOrWhiteSpace(ApiKey);

    public bool UsesBearerAuthentication() =>
        !string.IsNullOrWhiteSpace(ReadAccessToken);
}
