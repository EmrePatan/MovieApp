using Microsoft.Extensions.Options;
using MovieApp.Application.Abstractions.Identity;
using MovieApp.Application.Abstractions.Persistence;
using MovieApp.Application.Configuration;
using MovieApp.Application.Exceptions;
using MovieApp.Application.Identity;
using MovieApp.Application.Models.CatalogFollows;
using MovieApp.Application.Validation;

namespace MovieApp.Application.Services.CatalogFollows;

public sealed class GetCatalogUpcomingService(
    ICurrentUser currentUser,
    ICatalogFollowCatalogRepository catalogFollowCatalogRepository,
    IOptions<ReleaseRegionOptions> releaseRegionOptions) : IGetCatalogUpcomingService
{
    public async Task<CatalogUpcomingListResult> GetAsync(
        int page,
        int pageSize,
        CatalogUpcomingScope scope = CatalogUpcomingScope.Catalog,
        CancellationToken cancellationToken = default)
    {
        var validationResult = SearchPaginationValidator.Validate(page, pageSize);
        if (!validationResult.IsValid)
        {
            throw new ValidationException(validationResult.ErrorMessage!);
        }

        var today = DateOnly.FromDateTime(DateTime.UtcNow);
        var region = WatchProviderRegionValidator.Normalize(releaseRegionOptions.Value.DefaultRegion);

        (IReadOnlyList<CatalogUpcomingItemResult> items, int totalCount) result = scope switch
        {
            CatalogUpcomingScope.Followed => await GetFollowedAsync(page, pageSize, today, region, cancellationToken),
            _ => await GetCatalogAsync(page, pageSize, today, region, cancellationToken)
        };

        var (items, totalCount) = result;

        var totalPages = totalCount == 0
            ? 0
            : (int)Math.Ceiling(totalCount / (double)pageSize);

        return new CatalogUpcomingListResult(items, page, pageSize, totalCount, totalPages);
    }

    private async Task<(IReadOnlyList<CatalogUpcomingItemResult> Items, int TotalCount)> GetCatalogAsync(
        int page,
        int pageSize,
        DateOnly today,
        string region,
        CancellationToken cancellationToken)
    {
        Guid? userId = null;
        if (currentUser.IsAuthenticated)
        {
            userId = CurrentUserGuard.RequireUserId(currentUser);
        }

        return await catalogFollowCatalogRepository.GetUpcomingCatalogAsync(
            userId,
            page,
            pageSize,
            today,
            region,
            cancellationToken);
    }

    private async Task<(IReadOnlyList<CatalogUpcomingItemResult> Items, int TotalCount)> GetFollowedAsync(
        int page,
        int pageSize,
        DateOnly today,
        string region,
        CancellationToken cancellationToken)
    {
        if (!currentUser.IsAuthenticated)
        {
            throw new AuthenticationException("Authentication is required for followed upcoming catalog.");
        }

        var userId = CurrentUserGuard.RequireUserId(currentUser);
        return await catalogFollowCatalogRepository.GetFollowedUpcomingCatalogAsync(
            userId,
            page,
            pageSize,
            today,
            region,
            cancellationToken);
    }
}
