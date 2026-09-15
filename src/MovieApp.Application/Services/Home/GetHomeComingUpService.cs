using MovieApp.Application.Abstractions.Identity;
using MovieApp.Application.Abstractions.Persistence;
using MovieApp.Application.Identity;
using MovieApp.Application.Models.CatalogFollows;

namespace MovieApp.Application.Services.Home;

public sealed class GetHomeComingUpService(
    ICurrentUser currentUser,
    ICatalogFollowCatalogRepository catalogFollowCatalogRepository) : IGetHomeComingUpService
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

        return await catalogFollowCatalogRepository.GetFollowedTvUpcomingEpisodesAsync(
            userId,
            today,
            limit,
            cancellationToken);
    }
}
