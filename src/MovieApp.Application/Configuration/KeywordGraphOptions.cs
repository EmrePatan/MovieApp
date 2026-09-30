namespace MovieApp.Application.Configuration;

public sealed class KeywordGraphOptions
{
    public const string SectionName = "KeywordGraph";

    /// <summary>
    /// When false, TMDB sync uses legacy movie_keywords / tv_show_keywords replace semantics.
    /// TMDB movie_keyword_sources / tv_show_keyword_sources are still mirrored on each legacy
    /// sync (shadow provenance) so sources do not drift before this flag is enabled.
    /// Source tables are not authoritative for reads until this flag is true.
    /// </summary>
    public bool ProviderAwareSyncEnabled { get; set; }
}
