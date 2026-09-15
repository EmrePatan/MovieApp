using Microsoft.Extensions.Options;
using MovieApp.Application.Abstractions.Identity;
using MovieApp.Application.Abstractions.Persistence;
using MovieApp.Application.Configuration;
using MovieApp.Application.Identity;
using MovieApp.Application.Models.CatalogFollows;
using MovieApp.Application.Validation;

namespace MovieApp.Application.Services.Home;

public sealed class GetHomeComingUpService(
    ICurrentUser currentUser,
    ICatalogFollowCatalogRepository catalogFollowCatalogRepository,
    IOptions<ReleaseRegionOptions> releaseRegionOptions) : IGetHomeComingUpService
{
    public async Task<IReadOnlyList<CatalogUpcomingItemResult>> GetItemsAsync(
        int limit,
        CancellationToken cancellationToken = default)
    {
        if (!currentUser.IsAuthenticated)
        {
            return [];
        }

        if (limit <= 0)
        {
            return [];
        }

        var userId = CurrentUserGuard.RequireUserId(currentUser);
        var today = DateOnly.FromDateTime(DateTime.UtcNow);
        var region = WatchProviderRegionValidator.Normalize(releaseRegionOptions.Value.DefaultRegion);

        return await catalogFollowCatalogRepository.GetFollowedUpcomingForHomeAsync(
            userId,
            today,
            region,
            limit,
            cancellationToken);
    }
}
