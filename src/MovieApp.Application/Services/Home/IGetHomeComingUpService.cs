using MovieApp.Application.Models.CatalogFollows;

namespace MovieApp.Application.Services.Home;

public interface IGetHomeComingUpService
{
    Task<IReadOnlyList<CatalogUpcomingItemResult>> GetItemsAsync(
        int limit,
        string? releaseRegion,
        string contentLocale,
        CancellationToken cancellationToken = default);
}
