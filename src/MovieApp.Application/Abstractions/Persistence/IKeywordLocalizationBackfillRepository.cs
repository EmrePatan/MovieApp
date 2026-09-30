using MovieApp.Application.Services.Keywords;

namespace MovieApp.Application.Abstractions.Persistence;

public interface IKeywordLocalizationBackfillRepository
{
    Task<IReadOnlyList<KeywordLocalizationBackfillCandidate>> SelectCandidatesAsync(
        string locale,
        int take,
        Guid? startAfterKeywordId,
        CancellationToken cancellationToken = default);

    Task<KeywordLocalizationMachineUpsertResult> UpsertMachineTranslationsAsync(
        IReadOnlyList<KeywordLocalizationMachineUpsert> upserts,
        CancellationToken cancellationToken = default);

    Task UpsertCuratedTranslationAsync(
        KeywordLocalizationCuratedUpsert upsert,
        CancellationToken cancellationToken = default);

    Task<KeywordLocalizationDryRunCounts> GetDryRunCountsAsync(
        string locale,
        CancellationToken cancellationToken = default);

    Task<int> CountStaleProtectedAsync(
        string locale,
        CancellationToken cancellationToken = default);
}
