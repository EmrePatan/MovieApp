namespace MovieApp.Application.Configuration;

public sealed class KeywordGraphOptions
{
    public const string SectionName = "KeywordGraph";

    /// <summary>
    /// Retained for rollout/configuration compatibility. Keyword provenance now lives directly
    /// on movie_keywords / tv_show_keywords as embedded provider membership, so both sync paths
    /// preserve provider-aware ownership without the legacy source child tables.
    /// </summary>
    public bool ProviderAwareSyncEnabled { get; set; }
}
