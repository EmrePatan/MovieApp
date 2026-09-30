using MovieApp.Application.Models.Keywords;

namespace MovieApp.Application.Abstractions.Keywords;

public interface IKeywordGraphReconciliationService
{
    Task<KeywordGraphReconciliationResult> ReconcileAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// Read-only readiness verification for switch-time checks (no mutations).
    /// </summary>
    Task<KeywordGraphReconciliationResult> VerifyReadinessAsync(CancellationToken cancellationToken = default);
}
