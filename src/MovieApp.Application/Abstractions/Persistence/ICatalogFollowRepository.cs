using MovieApp.Domain.Entities;
using MovieApp.Domain.Enums;

namespace MovieApp.Application.Abstractions.Persistence;

public interface ICatalogFollowRepository
{
    Task<CatalogFollow?> GetForUserAndContentAsync(
        Guid userId,
        CatalogContentType contentType,
        Guid contentId,
        CancellationToken cancellationToken = default);

    Task<CatalogFollow?> GetForUserAndContentForUpdateAsync(
        Guid userId,
        CatalogContentType contentType,
        Guid contentId,
        CancellationToken cancellationToken = default);

    Task<bool> TryAddAsync(CatalogFollow follow, CancellationToken cancellationToken = default);

    Task SaveChangesAsync(CancellationToken cancellationToken = default);

    Task<bool> RemoveAsync(
        Guid userId,
        CatalogContentType contentType,
        Guid contentId,
        CancellationToken cancellationToken = default);

    Task<(IReadOnlyList<CatalogFollow> Follows, int TotalCount)> GetUserFollowsAsync(
        Guid userId,
        int page,
        int pageSize,
        CatalogContentType? contentType = null,
        CancellationToken cancellationToken = default);

    Task<IReadOnlyList<CatalogFollow>> GetEstablishedTvFollowsByTvShowIdsAsync(
        IReadOnlyCollection<Guid> tvShowIds,
        CancellationToken cancellationToken = default);

    Task<IReadOnlyList<CatalogFollow>> GetMovieFollowsForReleaseCheckAsync(
        CancellationToken cancellationToken = default);

    Task<IReadOnlyList<Guid>> GetFollowedMovieIdsAsync(
        CancellationToken cancellationToken = default);

    Task RemoveMovieFollowsByMovieIdAsync(
        Guid movieId,
        CancellationToken cancellationToken = default);
}
