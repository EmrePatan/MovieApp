namespace MovieApp.Application.Caching;

public interface IPersonalizedCacheRebuilder
{
    Task RebuildAsync(Guid userId, CancellationToken cancellationToken);
}
