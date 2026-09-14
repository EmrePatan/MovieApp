using MovieApp.Application.Models.CatalogFollows;

namespace MovieApp.Application.Services.CatalogFollows;

public interface IGetCatalogFollowsService
{
    Task<CatalogFollowsListResult> GetAsync(
        int page,
        int pageSize,
        CancellationToken cancellationToken = default);
}
