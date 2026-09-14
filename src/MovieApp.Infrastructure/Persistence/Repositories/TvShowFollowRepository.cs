using MovieApp.Application.Abstractions.Persistence;
using MovieApp.Domain.Entities;
using MovieApp.Domain.Enums;

namespace MovieApp.Infrastructure.Persistence.Repositories;

public sealed class TvShowFollowRepository(ICatalogFollowRepository catalogFollowRepository) : ITvShowFollowRepository
{
    public Task<CatalogFollow?> GetForUserAndTvShowAsync(
        Guid userId,
        Guid tvShowId,
        CancellationToken cancellationToken = default) =>
        catalogFollowRepository.GetForUserAndContentAsync(
            userId,
            CatalogContentType.Tv,
            tvShowId,
            cancellationToken);

    public Task<CatalogFollow?> GetForUserAndTvShowForUpdateAsync(
        Guid userId,
        Guid tvShowId,
        CancellationToken cancellationToken = default) =>
        catalogFollowRepository.GetForUserAndContentForUpdateAsync(
            userId,
            CatalogContentType.Tv,
            tvShowId,
            cancellationToken);

    public Task<bool> TryAddAsync(CatalogFollow follow, CancellationToken cancellationToken = default) =>
        catalogFollowRepository.TryAddAsync(follow, cancellationToken);

    public Task SaveChangesAsync(CancellationToken cancellationToken = default) =>
        catalogFollowRepository.SaveChangesAsync(cancellationToken);

    public Task<bool> RemoveForTvShowAsync(
        Guid userId,
        Guid tvShowId,
        CancellationToken cancellationToken = default) =>
        catalogFollowRepository.RemoveAsync(
            userId,
            CatalogContentType.Tv,
            tvShowId,
            cancellationToken);

    public Task<(IReadOnlyList<CatalogFollow> Follows, int TotalCount)> GetUserFollowsAsync(
        Guid userId,
        int page,
        int pageSize,
        CancellationToken cancellationToken = default) =>
        catalogFollowRepository.GetUserFollowsAsync(
            userId,
            page,
            pageSize,
            CatalogContentType.Tv,
            cancellationToken);
}
