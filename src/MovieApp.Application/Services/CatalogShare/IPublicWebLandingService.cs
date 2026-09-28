namespace MovieApp.Application.Services.CatalogShare;

public interface IPublicWebLandingService
{
    Task<PublicWebLandingPageModel> GetHomeAsync(string? acceptLanguageHeader, CancellationToken cancellationToken = default);

    Task<PublicWebCatalogListingPageModel> GetMoviesListingAsync(
        string? acceptLanguageHeader,
        CancellationToken cancellationToken = default);

    Task<PublicWebCatalogListingPageModel> GetTvListingAsync(
        string? acceptLanguageHeader,
        CancellationToken cancellationToken = default);
}
