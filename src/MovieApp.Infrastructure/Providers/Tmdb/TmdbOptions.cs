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

    /// <summary>
    /// Upper bound for a single TMDB HTTP attempt (connect, TLS, response).
    /// </summary>
    public int RequestTimeoutSeconds { get; set; } = 10;

    /// <summary>
    /// Upper bound for one logical TMDB call including retries and backoff. Must stay well below
    /// the mobile client's 30s request timeout so callers receive a 503 instead of a dropped request.
    /// </summary>
    public int RequestBudgetSeconds { get; set; } = 20;

    public bool IsConfigured() =>
        !string.IsNullOrWhiteSpace(ReadAccessToken) || !string.IsNullOrWhiteSpace(ApiKey);

    public bool UsesBearerAuthentication() =>
        !string.IsNullOrWhiteSpace(ReadAccessToken);
}
