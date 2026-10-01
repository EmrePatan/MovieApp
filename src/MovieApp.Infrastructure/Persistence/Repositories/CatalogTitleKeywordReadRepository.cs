using Microsoft.EntityFrameworkCore;
using MovieApp.Application.Abstractions.Persistence;
using MovieApp.Infrastructure.Persistence;

using MovieApp.Application.Models.Catalog;

using MovieApp.Application.Services.Keywords;

using MovieApp.Application.Services.Localization;



namespace MovieApp.Infrastructure.Persistence.Repositories;



public sealed class CatalogTitleKeywordReadRepository(ApplicationDbContext dbContext)

    : ICatalogTitleKeywordReadRepository

{

    public Task<IReadOnlyList<CatalogKeywordSummary>> GetLocalizedKeywordsForMovieAsync(

        Guid movieId,

        string contentLocale,

        int maxCount,

        CancellationToken cancellationToken = default) =>

        LoadLocalizedKeywordsAsync(

            dbContext.MovieKeywords.AsNoTracking().Where(join => join.MovieId == movieId),

            contentLocale,

            maxCount,

            cancellationToken);



    public Task<IReadOnlyList<CatalogKeywordSummary>> GetLocalizedKeywordsForTvShowAsync(

        Guid tvShowId,

        string contentLocale,

        int maxCount,

        CancellationToken cancellationToken = default) =>

        LoadLocalizedKeywordsAsync(

            dbContext.TvShowKeywords.AsNoTracking().Where(join => join.TvShowId == tvShowId),

            contentLocale,

            maxCount,

            cancellationToken);



    private static async Task<IReadOnlyList<CatalogKeywordSummary>> LoadLocalizedKeywordsAsync(

        IQueryable<Domain.Entities.MovieKeyword> movieJoins,

        string contentLocale,

        int maxCount,

        CancellationToken cancellationToken)

    {

        if (maxCount <= 0)

        {

            return [];

        }



        var locale = KeywordDiscoverLocalizationSupport.NormalizeLocale(contentLocale);

        var englishLocale = SupportedContentLocales.EnglishUnitedStates;



        var rows = await movieJoins

            .Where(join =>

                join.Keyword.DisplayProfile != null &&

                join.Keyword.DisplayProfile.Displayable)

            .OrderByDescending(join => join.Keyword.DisplayProfile!.DisplayRank)

            .Select(join => new KeywordRowProjection(

                join.KeywordId,

                join.Keyword.Name,

                join.Keyword.CanonicalName,

                join.Keyword.Localizations

                    .Where(localization => localization.Locale == locale)

                    .Select(localization => localization.Name)

                    .FirstOrDefault(),

                join.Keyword.Localizations

                    .Where(localization => localization.Locale == englishLocale)

                    .Select(localization => localization.Name)

                    .FirstOrDefault()))

            .Take(maxCount)

            .ToListAsync(cancellationToken);



        return ProjectKeywordSummaries(rows, maxCount);

    }



    private static async Task<IReadOnlyList<CatalogKeywordSummary>> LoadLocalizedKeywordsAsync(

        IQueryable<Domain.Entities.TvShowKeyword> tvJoins,

        string contentLocale,

        int maxCount,

        CancellationToken cancellationToken)

    {

        if (maxCount <= 0)

        {

            return [];

        }



        var locale = KeywordDiscoverLocalizationSupport.NormalizeLocale(contentLocale);

        var englishLocale = SupportedContentLocales.EnglishUnitedStates;



        var rows = await tvJoins

            .Where(join =>

                join.Keyword.DisplayProfile != null &&

                join.Keyword.DisplayProfile.Displayable)

            .OrderByDescending(join => join.Keyword.DisplayProfile!.DisplayRank)

            .Select(join => new KeywordRowProjection(

                join.KeywordId,

                join.Keyword.Name,

                join.Keyword.CanonicalName,

                join.Keyword.Localizations

                    .Where(localization => localization.Locale == locale)

                    .Select(localization => localization.Name)

                    .FirstOrDefault(),

                join.Keyword.Localizations

                    .Where(localization => localization.Locale == englishLocale)

                    .Select(localization => localization.Name)

                    .FirstOrDefault()))

            .Take(maxCount)

            .ToListAsync(cancellationToken);



        return ProjectKeywordSummaries(rows, maxCount);

    }



    private static List<CatalogKeywordSummary> ProjectKeywordSummaries(

        IReadOnlyList<KeywordRowProjection> rows,

        int maxCount) =>

        rows

            .Select(row =>

            {

                var name = KeywordDiscoverLocalizationSupport.ResolveDisplayName(

                    row.RequestedLocaleName,

                    row.EnglishLocaleName,

                    row.CanonicalName,

                    row.Name);



                return string.IsNullOrWhiteSpace(name)

                    ? null

                    : new CatalogKeywordSummary(row.KeywordId, name);

            })

            .Where(summary => summary is not null)

            .Select(summary => summary!)

            .GroupBy(summary => summary.Id)

            .Select(group => group.First())

            .Take(maxCount)

            .ToList();



    private sealed record KeywordRowProjection(

        Guid KeywordId,

        string Name,

        string? CanonicalName,

        string? RequestedLocaleName,

        string? EnglishLocaleName);

}


