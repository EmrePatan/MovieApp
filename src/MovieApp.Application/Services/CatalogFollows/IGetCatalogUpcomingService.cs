using MovieApp.Application.Models.CatalogFollows;

namespace MovieApp.Application.Services.CatalogFollows;

public interface IGetCatalogUpcomingService
{
    Task<CatalogUpcomingListResult> GetAsync(
        int page,
        int pageSize,
        CatalogUpcomingScope scope = CatalogUpcomingScope.Catalog,
        CancellationToken cancellationToken = default);
}
