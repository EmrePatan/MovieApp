namespace MovieApp.Application.Services.Keywords;

public interface IKeywordLocalizationBackfillService
{
    Task<KeywordLocalizationBackfillResult> BackfillAsync(
        KeywordLocalizationBackfillRequest request,
        CancellationToken cancellationToken = default);

    Task<KeywordLocalizationDryRunPlan> PlanDryRunAsync(
        KeywordLocalizationBackfillRequest request,
        CancellationToken cancellationToken = default);
}
