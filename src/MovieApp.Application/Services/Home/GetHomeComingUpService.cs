using Microsoft.Extensions.Options;
using MovieApp.Application.Abstractions.Identity;
using MovieApp.Application.Abstractions.Persistence;
using MovieApp.Application.Configuration;
using MovieApp.Application.Identity;
using MovieApp.Application.Models.CatalogFollows;
using MovieApp.Application.Services.Localization;
using MovieApp.Application.Validation;

namespace MovieApp.Application.Services.Home;

public sealed class GetHomeComingUpService(
    ICurrentUser currentUser,
    ICatalogFollowCatalogRepository catalogFollowCatalogRepository,
    ISummaryLocalizationOverlayService summaryLocalizationOverlayService,
    IOptions<ReleaseRegionOptions> releaseRegionOptions) : IGetHomeComingUpService
{
    public async Task<IReadOnlyList<CatalogUpcomingItemResult>> GetItemsAsync(
        int limit,
        string? releaseRegion,
        string contentLocale,
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
        var region = ResolveRegion(releaseRegion);

        var items = await catalogFollowCatalogRepository.GetFollowedUpcomingForHomeAsync(
            userId,
            today,
            region,
            limit,
            cancellationToken);

        return await summaryLocalizationOverlayService.ApplyToUpcomingItemsAsync(
            items,
            contentLocale,
            cancellationToken);
    }

    private string ResolveRegion(string? releaseRegion)
    {
        if (!string.IsNullOrWhiteSpace(releaseRegion))
        {
            return WatchProviderRegionValidator.Normalize(releaseRegion);
        }

        return WatchProviderRegionValidator.Normalize(releaseRegionOptions.Value.DefaultRegion);
    }
}
