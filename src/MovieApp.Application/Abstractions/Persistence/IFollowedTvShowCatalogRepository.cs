namespace MovieApp.Application.Abstractions.Persistence;

public interface IFollowedTvShowCatalogRepository
{
    Task<IReadOnlyDictionary<int, Guid>> GetFollowedTvShowIdsByTmdbIdAsync(
        CancellationToken cancellationToken = default);
}
