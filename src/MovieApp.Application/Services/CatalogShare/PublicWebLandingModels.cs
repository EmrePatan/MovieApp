namespace MovieApp.Application.Services.CatalogShare;

public sealed record PublicWebLandingPageModel(
    string CanonicalPageUrl,
    string OpenAppUrl,
    IReadOnlyList<PublicWebCatalogCard> TrendingMovies,
    IReadOnlyList<PublicWebCatalogCard> TrendingTvShows);

public sealed record PublicWebCatalogListingPageModel(
    string CanonicalPageUrl,
    string PageTitle,
    string PageDescription,
    string ContentKind,
    IReadOnlyList<PublicWebCatalogCard> Items);
