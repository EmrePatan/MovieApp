namespace MovieApp.Application.Models.Keywords;

public sealed class KeywordGraphReconciliationResult
{
    public int KeywordsProcessed { get; init; }

    public int CanonicalNamesUpdated { get; init; }

    public int NormalizedNamesUpdated { get; init; }

    public int ExternalReferencesCreated { get; init; }

    public int MovieSourcesCreated { get; init; }

    public int TvSourcesCreated { get; init; }

    public int KeywordCount { get; init; }

    public int TmdbExternalReferenceCount { get; init; }

    public int MissingCanonicalNameCount { get; init; }

    public int MissingNormalizedNameCount { get; init; }

    public int MissingTmdbExternalRefCount { get; init; }

    public int MovieKeywordJoinCount { get; init; }

    public int MissingMovieTmdbSourceCount { get; init; }

    public int TvShowKeywordJoinCount { get; init; }

    public int MissingTvTmdbSourceCount { get; init; }

    public int ConflictingExternalReferenceCount { get; init; }

    public IReadOnlyList<string> Conflicts { get; init; } = [];

    public bool IsReadyForProviderAwareSync { get; init; }
}
