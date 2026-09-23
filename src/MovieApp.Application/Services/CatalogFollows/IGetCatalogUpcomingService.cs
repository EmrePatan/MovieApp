using MovieApp.Application.Models.CatalogFollows;
using MovieApp.Application.Services.Localization;

namespace MovieApp.Application.Services.CatalogFollows;

public interface IGetCatalogUpcomingService
{
    Task<CatalogUpcomingListResult> GetAsync(
        int page,
        int pageSize,
        CatalogUpcomingScope scope = CatalogUpcomingScope.Catalog,
        string? releaseRegion = null,
        string contentLocale = ContentLocaleResolver.EnglishUnitedStates,
        CancellationToken cancellationToken = default);
}
