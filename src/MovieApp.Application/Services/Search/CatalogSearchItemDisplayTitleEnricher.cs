using MovieApp.Application.Abstractions.Persistence;
using MovieApp.Application.Models.Movies;
using MovieApp.Application.Models.Search;
using MovieApp.Application.Services.Localization;
using MovieApp.Domain.Enums;

namespace MovieApp.Application.Services.Search;

public sealed class CatalogSearchItemDisplayTitleEnricher(
    IContentSearchTitleReadRepository contentSearchTitleReadRepository,
    IMovieRepository movieRepository,
    ITvShowRepository tvShowRepository)
{
    public async Task<PaginatedResult<SearchItem>> EnrichAsync(
        PaginatedResult<SearchItem> page,
        string contentLocale,
        CancellationToken cancellationToken = default)
    {
        if (!ContentLocaleResolver.RequiresLocalization(contentLocale) || page.Items.Count == 0)
        {
            return page;
        }

        var movieIds = page.Items
            .Where(item => string.Equals(item.Type, "movie", StringComparison.OrdinalIgnoreCase))
            .Select(item => item.Id)
            .Distinct()
            .ToList();
        var tvIds = page.Items
            .Where(item => string.Equals(item.Type, "tv", StringComparison.OrdinalIgnoreCase))
            .Select(item => item.Id)
            .Distinct()
            .ToList();

        var localizedMovieTitles = movieIds.Count == 0
            ? new Dictionary<CatalogContentKey, string>()
            : await contentSearchTitleReadRepository.GetLocaleDisplayTitlesAsync(
                CatalogContentType.Movie,
                movieIds,
                contentLocale,
                cancellationToken);
        var localizedTvTitles = tvIds.Count == 0
            ? new Dictionary<CatalogContentKey, string>()
            : await contentSearchTitleReadRepository.GetLocaleDisplayTitlesAsync(
                CatalogContentType.Tv,
                tvIds,
                contentLocale,
                cancellationToken);

        var movieContexts = await movieRepository.GetProductionContextsByIdsAsync(movieIds, cancellationToken);
        var tvContexts = await tvShowRepository.GetProductionContextsByIdsAsync(tvIds, cancellationToken);

        var items = page.Items
            .Select(item => ApplyItem(
                item,
                contentLocale,
                localizedMovieTitles,
                localizedTvTitles,
                movieContexts,
                tvContexts))
            .ToList();

        return page with { Items = items };
    }

    private static SearchItem ApplyItem(
        SearchItem item,
        string contentLocale,
        IReadOnlyDictionary<CatalogContentKey, string> localizedMovieTitles,
        IReadOnlyDictionary<CatalogContentKey, string> localizedTvTitles,
        IReadOnlyDictionary<Guid, ContentProductionContext> movieContexts,
        IReadOnlyDictionary<Guid, ContentProductionContext> tvContexts)
    {
        if (string.Equals(item.Type, "movie", StringComparison.OrdinalIgnoreCase))
        {
            movieContexts.TryGetValue(item.Id, out var context);
            localizedMovieTitles.TryGetValue(new CatalogContentKey(item.Id, "movie"), out var localizedTitle);
            var titles = LocalizedDisplayTitleSelector.ChooseDisplayTitles(
                item.Title,
                item.OriginalTitle,
                context.OriginalLanguage,
                localizedTitle,
                contentLocale,
                context.PrimaryOriginCountryCode);
            return item with { Title = titles.Title, OriginalTitle = titles.OriginalTitle };
        }

        if (string.Equals(item.Type, "tv", StringComparison.OrdinalIgnoreCase))
        {
            tvContexts.TryGetValue(item.Id, out var context);
            localizedTvTitles.TryGetValue(new CatalogContentKey(item.Id, "tv"), out var localizedTitle);
            var titles = LocalizedDisplayTitleSelector.ChooseDisplayTitles(
                item.Title,
                item.OriginalTitle,
                context.OriginalLanguage,
                localizedTitle,
                contentLocale,
                context.PrimaryOriginCountryCode);
            return item with { Title = titles.Title, OriginalTitle = titles.OriginalTitle };
        }

        return item;
    }
}
