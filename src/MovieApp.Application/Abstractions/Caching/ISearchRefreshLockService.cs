namespace MovieApp.Application.Abstractions.Caching;

public enum SearchRefreshLockBackend
{
    Redis,
    LocalSingleFlight
}

public interface ISearchRefreshLockService
{
    Task<SearchRefreshLockHandle?> TryAcquireAsync(
        string lockKey,
        TimeSpan lockDuration,
        CancellationToken cancellationToken = default);

    Task ReleaseAsync(
        string lockKey,
        string lockToken,
        SearchRefreshLockBackend backend,
        CancellationToken cancellationToken = default);
}

public sealed class SearchRefreshLockHandle(
    string lockKey,
    string lockToken,
    SearchRefreshLockBackend backend) : IAsyncDisposable
{
    public string LockKey { get; } = lockKey;

    public string LockToken { get; } = lockToken;

    public SearchRefreshLockBackend Backend { get; } = backend;

    public ValueTask DisposeAsync() => ValueTask.CompletedTask;
}
