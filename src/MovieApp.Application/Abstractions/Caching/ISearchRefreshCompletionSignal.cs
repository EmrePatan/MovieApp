namespace MovieApp.Application.Abstractions.Caching;

public enum SearchRefreshAttemptOutcome
{
    Succeeded,
    Failed
}

public interface ISearchRefreshCompletionSignal
{
    Task PublishAsync(
        string lockKey,
        SearchRefreshAttemptOutcome outcome,
        TimeSpan ttl,
        CancellationToken cancellationToken = default);

    Task<SearchRefreshAttemptOutcome?> TryGetOutcomeAsync(
        string lockKey,
        CancellationToken cancellationToken = default);
}
