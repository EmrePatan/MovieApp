using MovieApp.Application.Models.Movies;
using MovieApp.Application.Models.Providers;
using MovieApp.Application.Models.Search;
using MovieApp.Application.Services.Localization;

namespace MovieApp.Application.Services.Search;

internal static class LocalizedCatalogDisplayPolicy
{
    internal static PaginatedResult<SearchItem> ApplyToSearchResults(
        PaginatedResult<SearchItem> page,
        string contentLocale,
        MovieProviderSearchResult? localizedMovies,
        MovieProviderSearchResult? canonicalMovies,
        TvShowProviderSearchResult? localizedTv,
        TvShowProviderSearchResult? canonicalTv)
    {
        if (!ContentLocaleResolver.RequiresLocalization(contentLocale) || page.Items.Count == 0)
        {
            return page;
        }

        var canonicalMovieLookup = BuildMovieLookup(canonicalMovies);
        var localizedMovieLookup = BuildMovieLookup(localizedMovies);
        var canonicalTvLookup = BuildTvLookup(canonicalTv);
        var localizedTvLookup = BuildTvLookup(localizedTv);

        var items = page.Items
            .Select(item => ApplyItem(
                item,
                contentLocale,
                canonicalMovieLookup,
                localizedMovieLookup,
                canonicalTvLookup,
                localizedTvLookup))
            .ToList();

        return page with { Items = items };
    }

    private static SearchItem ApplyItem(
        SearchItem item,
        string contentLocale,
        Dictionary<int, MovieProviderSummary> canonicalMovies,
        Dictionary<int, MovieProviderSummary> localizedMovies,
        Dictionary<int, TvShowProviderSummary> canonicalTv,
        Dictionary<int, TvShowProviderSummary> localizedTv)
    {
        if (string.Equals(item.Type, "movie", StringComparison.OrdinalIgnoreCase) &&
            item.TmdbId is int movieTmdbId &&
            canonicalMovies.TryGetValue(movieTmdbId, out var canonicalMovie))
        {
            localizedMovies.TryGetValue(movieTmdbId, out var localizedMovie);
            localizedMovie ??= canonicalMovie;

            var titles = LocalizedDisplayTitleSelector.ChooseDisplayTitles(
                canonicalMovie.Title,
                canonicalMovie.OriginalTitle,
                canonicalMovie.OriginalLanguage,
                string.Equals(localizedMovie.Title, canonicalMovie.Title, StringComparison.Ordinal)
                    ? null
                    : localizedMovie.Title,
                contentLocale,
                canonicalMovie.PrimaryOriginCountryCode);

            var posterUrl = ResolveTurkishProductionPosterUrl(
                canonicalMovie,
                localizedMovie,
                item.PosterUrl);

            return item with
            {
                Title = titles.Title,
                OriginalTitle = titles.OriginalTitle,
                PosterUrl = posterUrl,
            };
        }

        if (string.Equals(item.Type, "tv", StringComparison.OrdinalIgnoreCase) &&
            item.TmdbId is int tvTmdbId &&
            canonicalTv.TryGetValue(tvTmdbId, out var canonicalShow))
        {
            localizedTv.TryGetValue(tvTmdbId, out var localizedShow);
            localizedShow ??= canonicalShow;

            var titles = LocalizedDisplayTitleSelector.ChooseDisplayTitles(
                canonicalShow.Title,
                canonicalShow.OriginalTitle,
                canonicalShow.OriginalLanguage,
                string.Equals(localizedShow.Title, canonicalShow.Title, StringComparison.Ordinal)
                    ? null
                    : localizedShow.Title,
                contentLocale,
                canonicalShow.PrimaryOriginCountryCode);

            var posterUrl = ResolveTurkishProductionPosterUrl(
                canonicalShow,
                localizedShow,
                item.PosterUrl);

            return item with
            {
                Title = titles.Title,
                OriginalTitle = titles.OriginalTitle,
                PosterUrl = posterUrl,
            };
        }

        return item;
    }

    private static string? ResolveTurkishProductionPosterUrl(
        MovieProviderSummary canonical,
        MovieProviderSummary localized,
        string? fallbackPosterUrl) =>
        ResolveTurkishProductionPosterUrl(
            canonical.OriginalLanguage,
            canonical.PrimaryOriginCountryCode,
            canonical.OriginalTitle,
            localized.PosterPath,
            canonical.PosterPath,
            fallbackPosterUrl);

    private static string? ResolveTurkishProductionPosterUrl(
        TvShowProviderSummary canonical,
        TvShowProviderSummary localized,
        string? fallbackPosterUrl) =>
        ResolveTurkishProductionPosterUrl(
            canonical.OriginalLanguage,
            canonical.PrimaryOriginCountryCode,
            canonical.OriginalTitle,
            localized.PosterPath,
            canonical.PosterPath,
            fallbackPosterUrl);

    private static string? ResolveTurkishProductionPosterUrl(
        string? originalLanguage,
        string? primaryOriginCountryCode,
        string? originalTitle,
        string? localizedPosterPath,
        string? canonicalPosterPath,
        string? fallbackPosterUrl)
    {
        if (!LocalizedDisplayTitleSelector.IsTurkishProduction(
                originalLanguage,
                primaryOriginCountryCode,
                originalTitle))
        {
            return canonicalPosterPath ?? fallbackPosterUrl;
        }

        return localizedPosterPath ?? canonicalPosterPath ?? fallbackPosterUrl;
    }

    private static Dictionary<int, MovieProviderSummary> BuildMovieLookup(
        MovieProviderSearchResult? result) =>
        result?.Results
            .Where(summary => summary.TmdbId is > 0)
            .GroupBy(summary => summary.TmdbId!.Value)
            .ToDictionary(group => group.Key, group => group.Last())
        ?? new Dictionary<int, MovieProviderSummary>();

    private static Dictionary<int, TvShowProviderSummary> BuildTvLookup(
        TvShowProviderSearchResult? result) =>
        result?.Results
            .Where(summary => summary.TmdbId is > 0)
            .GroupBy(summary => summary.TmdbId!.Value)
            .ToDictionary(group => group.Key, group => group.Last())
        ?? new Dictionary<int, TvShowProviderSummary>();
}
