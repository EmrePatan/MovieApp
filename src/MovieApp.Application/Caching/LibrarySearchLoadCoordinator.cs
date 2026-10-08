namespace MovieApp.Application.Caching;

/// <summary>
/// Coalesces in-process library search misses that share a user, generation, and query key.
/// </summary>
public sealed class LibrarySearchLoadCoordinator
{
    private readonly InProcessLoadCoordinator<Models.Movies.PaginatedResult<Models.Library.LibraryItemResult>> _loads = new();

    public Task<Models.Movies.PaginatedResult<Models.Library.LibraryItemResult>> RunAsync(
        string cacheKey,
        Func<Task<Models.Movies.PaginatedResult<Models.Library.LibraryItemResult>>> load) =>
        _loads.RunInFlightAsync(cacheKey, load);
}
