namespace MovieApp.Application.Abstractions.Caching;

public interface ISearchRefreshLockService
{
    Task<SearchRefreshLockHandle?> TryAcquireAsync(
        string lockKey,
        TimeSpan lockDuration,
        CancellationToken cancellationToken = default);

    Task ReleaseAsync(
        string lockKey,
        string lockToken,
        CancellationToken cancellationToken = default);
}

public sealed class SearchRefreshLockHandle(string lockKey, string lockToken) : IAsyncDisposable
{
    public string LockKey { get; } = lockKey;

    public string LockToken { get; } = lockToken;

    public ValueTask DisposeAsync() => ValueTask.CompletedTask;
}
