using Microsoft.Extensions.Options;
using MovieApp.Application.Configuration;
using MovieApp.Application.Models.Search;
using MovieApp.Application.Services.Search;

namespace MovieApp.Application.Services.CatalogShare;

public sealed class PublicWebLandingService(
    IDiscoveryService discoveryService,
    IOptions<CatalogShareOptions> catalogShareOptions) : IPublicWebLandingService
{
    private const int HomeRailSize = 10;
    private const int ListingPageSize = 24;

    public async Task<PublicWebLandingPageModel> GetHomeAsync(
        string? acceptLanguageHeader,
        CancellationToken cancellationToken = default)
    {
        var locale = acceptLanguageHeader ?? string.Empty;
        var movies = await LoadTrendingAsync(SearchContentType.Movie, HomeRailSize, locale, cancellationToken);
        var tv = await LoadTrendingAsync(SearchContentType.Tv, HomeRailSize, locale, cancellationToken);
        var options = catalogShareOptions.Value;
        var canonical = options.PublicWebBaseUrl.TrimEnd('/');

        var openAppUrl = movies.Count > 0
            ? CatalogShareWebUrls.BuildAppOpenUrl(options, "movie", movies[0].Id)
            : tv.Count > 0
                ? CatalogShareWebUrls.BuildAppOpenUrl(options, "tv", tv[0].Id)
                : $"{canonical}/movies";

        return new PublicWebLandingPageModel($"{canonical}/", openAppUrl, movies, tv);
    }

    public async Task<PublicWebCatalogListingPageModel> GetMoviesListingAsync(
        string? acceptLanguageHeader,
        CancellationToken cancellationToken = default)
    {
        var copy = PublicWebSiteCopy.Resolve(acceptLanguageHeader);
        var locale = acceptLanguageHeader ?? string.Empty;
        var items = await LoadTrendingAsync(SearchContentType.Movie, ListingPageSize, locale, cancellationToken);
        var canonical = catalogShareOptions.Value.PublicWebBaseUrl.TrimEnd('/');

        return new PublicWebCatalogListingPageModel(
            $"{canonical}/movies",
            copy.MoviesPageTitle,
            copy.MoviesPageDescription,
            "movie",
            items);
    }

    public async Task<PublicWebCatalogListingPageModel> GetTvListingAsync(
        string? acceptLanguageHeader,
        CancellationToken cancellationToken = default)
    {
        var copy = PublicWebSiteCopy.Resolve(acceptLanguageHeader);
        var locale = acceptLanguageHeader ?? string.Empty;
        var items = await LoadTrendingAsync(SearchContentType.Tv, ListingPageSize, locale, cancellationToken);
        var canonical = catalogShareOptions.Value.PublicWebBaseUrl.TrimEnd('/');

        return new PublicWebCatalogListingPageModel(
            $"{canonical}/tv",
            copy.TvPageTitle,
            copy.TvPageDescription,
            "tv",
            items);
    }

    private async Task<IReadOnlyList<PublicWebCatalogCard>> LoadTrendingAsync(
        SearchContentType type,
        int pageSize,
        string contentLocale,
        CancellationToken cancellationToken)
    {
        var criteria = new DiscoveryCriteria(type, Page: 1, PageSize: pageSize);
        var page = await discoveryService.GetTrendingAsync(criteria, contentLocale, cancellationToken);
        var options = catalogShareOptions.Value;
        var segment = type == SearchContentType.Movie ? "movie" : "tv";

        return page.Items
            .Select(item => new PublicWebCatalogCard(
                item.Id,
                item.Title,
                item.Year ?? item.ReleaseDate?.Year,
                item.VoteAverage,
                item.PosterUrl,
                CatalogShareWebUrls.BuildCanonicalUrl(options, segment, item.Id)))
            .ToList();
    }
}
