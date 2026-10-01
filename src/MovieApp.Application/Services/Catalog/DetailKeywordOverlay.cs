using MovieApp.Application.Abstractions.Persistence;
using MovieApp.Application.Models.Movies;
using MovieApp.Application.Models.TvShows;
using MovieApp.Application.Services.Localization;

namespace MovieApp.Application.Services.Catalog;

public static class DetailKeywordOverlay
{
    public static async Task<MovieDetailsResult> ApplyMovieKeywordsAsync(
        ICatalogTitleKeywordReadRepository keywordReadRepository,
        Guid movieId,
        MovieDetailsResult result,
        string? contentLocale,
        CancellationToken cancellationToken)
    {
        var locale = string.IsNullOrWhiteSpace(contentLocale)
            ? SupportedContentLocales.EnglishUnitedStates
            : contentLocale;

        var keywords = await keywordReadRepository.GetLocalizedKeywordNamesForMovieAsync(
            movieId,
            locale,
            CatalogDisplayLimits.MaxDetailKeywords,
            cancellationToken);

        return result with { Keywords = keywords };
    }

    public static async Task<TvShowDetailsResult> ApplyTvShowKeywordsAsync(
        ICatalogTitleKeywordReadRepository keywordReadRepository,
        Guid tvShowId,
        TvShowDetailsResult result,
        string? contentLocale,
        CancellationToken cancellationToken)
    {
        var locale = string.IsNullOrWhiteSpace(contentLocale)
            ? SupportedContentLocales.EnglishUnitedStates
            : contentLocale;

        var keywords = await keywordReadRepository.GetLocalizedKeywordNamesForTvShowAsync(
            tvShowId,
            locale,
            CatalogDisplayLimits.MaxDetailKeywords,
            cancellationToken);

        return result with { Keywords = keywords };
    }
}
