namespace MovieApp.Application.Abstractions.Keywords;

public interface IKeywordDisplayProfileRefreshService
{
    Task<KeywordDisplayProfileRefreshResult> RefreshAsync(CancellationToken cancellationToken = default);
}

public sealed record KeywordDisplayProfileRefreshResult(
    bool Succeeded,
    int ProfilesWritten,
    int DisplayableCount,
    long DurationMilliseconds,
    string? FailureReason);
