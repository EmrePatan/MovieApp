using MovieApp.Domain.Entities;

namespace MovieApp.Application.Abstractions.Persistence;

public interface ITvShowFollowRepository
{
    Task<TvShowFollow?> GetForUserAndTvShowAsync(
        Guid userId,
        Guid tvShowId,
        CancellationToken cancellationToken = default);

    Task<TvShowFollow?> GetForUserAndTvShowForUpdateAsync(
        Guid userId,
        Guid tvShowId,
        CancellationToken cancellationToken = default);

    Task<bool> TryAddAsync(TvShowFollow follow, CancellationToken cancellationToken = default);

    Task SaveChangesAsync(CancellationToken cancellationToken = default);

    Task<bool> RemoveForTvShowAsync(
        Guid userId,
        Guid tvShowId,
        CancellationToken cancellationToken = default);

    Task<(IReadOnlyList<TvShowFollow> Follows, int TotalCount)> GetUserFollowsAsync(
        Guid userId,
        int page,
        int pageSize,
        CancellationToken cancellationToken = default);
}
