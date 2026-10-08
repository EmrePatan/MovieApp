namespace MovieApp.Application.Caching;

/// <summary>
/// Coalesces per-user home and insights rebuilds after a generation bump.
/// </summary>
public interface IPersonalizedCacheRebuildScheduler
{
    void Schedule(Guid userId);
}
