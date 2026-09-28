namespace MovieApp.Application.Caching;

public static class TvLocalizedPosterMissCacheKeys
{
    public static readonly TimeSpan Ttl = TimeSpan.FromHours(6);

    public static string Create(Guid tvShowId, string languageKey) =>
        $"tv-loc-poster-miss:{tvShowId:N}:{languageKey}";
}

public sealed class TvLocalizedPosterMissCacheEntry
{
    public bool Miss { get; init; } = true;
}
