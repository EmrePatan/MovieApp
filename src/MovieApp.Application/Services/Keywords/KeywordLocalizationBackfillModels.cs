namespace MovieApp.Application.Services.Keywords;

public enum KeywordLocalizationBackfillCandidateAction
{
    Insert = 0,
    UpdateStaleMachine = 1,
}

public sealed record KeywordLocalizationBackfillCandidate(
    Guid KeywordId,
    string SourceText,
    string SourceTextHash,
    KeywordLocalizationBackfillCandidateAction Action);

public sealed record KeywordLocalizationMachineUpsert(
    Guid KeywordId,
    string Locale,
    string Name,
    string NormalizedName,
    string SourceTextHash,
    DateTime UtcNow);

public sealed record KeywordLocalizationCuratedUpsert(
    Guid KeywordId,
    string Locale,
    string Name,
    string NormalizedName,
    string SourceTextHash,
    DateTime UtcNow);

public sealed record KeywordLocalizationBackfillRequest(
    string Locale,
    int BatchSize,
    int MaxItems,
    int DelayBetweenBatchesMs,
    int MaxConcurrency,
    bool DryRun,
    Guid? StartAfterKeywordId = null);

public sealed record KeywordLocalizationBackfillResult(
    string Locale,
    int Selected,
    int Translated,
    int Inserted,
    int Updated,
    int SkippedCurrent,
    int SkippedCurated,
    int StaleReviewRequired,
    int Failed,
    int ProviderRequests,
    long ElapsedMilliseconds,
    bool DryRun);

public sealed record KeywordLocalizationDryRunPlan(
    string Locale,
    int EligibleCandidates,
    int Missing,
    int StaleMachine,
    int ProtectedCuratedOrReviewed,
    int WouldTranslate);

public sealed record KeywordLocalizationMachineUpsertResult(
    int Inserted,
    int Updated,
    int SkippedProtected);

public sealed record KeywordLocalizationDryRunCounts(
    int Missing,
    int StaleMachine,
    int Current,
    int ProtectedCuratedOrReviewed);

public sealed record KeywordLocalizationLocaleCoverage(
    string Locale,
    int Eligible,
    int Localized,
    decimal CoveragePercent,
    int Missing,
    int MachineUnreviewed,
    int Reviewed,
    int Curated,
    int Stale);
