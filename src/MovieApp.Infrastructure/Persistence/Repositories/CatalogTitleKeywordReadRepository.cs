using Microsoft.EntityFrameworkCore;
using MovieApp.Application.Abstractions.Persistence;
using MovieApp.Application.Services.Keywords;
using MovieApp.Application.Services.Localization;

namespace MovieApp.Infrastructure.Persistence.Repositories;

public sealed class CatalogTitleKeywordReadRepository(ApplicationDbContext dbContext)
    : ICatalogTitleKeywordReadRepository
{
    public Task<IReadOnlyList<string>> GetLocalizedKeywordNamesForMovieAsync(
        Guid movieId,
        string contentLocale,
        int maxCount,
        CancellationToken cancellationToken = default) =>
        LoadLocalizedKeywordNamesAsync(
            dbContext.MovieKeywords.AsNoTracking().Where(join => join.MovieId == movieId),
            contentLocale,
            maxCount,
            cancellationToken);

    public Task<IReadOnlyList<string>> GetLocalizedKeywordNamesForTvShowAsync(
        Guid tvShowId,
        string contentLocale,
        int maxCount,
        CancellationToken cancellationToken = default) =>
        LoadLocalizedKeywordNamesAsync(
            dbContext.TvShowKeywords.AsNoTracking().Where(join => join.TvShowId == tvShowId),
            contentLocale,
            maxCount,
            cancellationToken);

    private static async Task<IReadOnlyList<string>> LoadLocalizedKeywordNamesAsync(
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
            .Select(join => new KeywordNameProjection(
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
            .ToListAsync(cancellationToken);

        return ProjectKeywordNames(rows, maxCount);
    }

    private static async Task<IReadOnlyList<string>> LoadLocalizedKeywordNamesAsync(
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
            .Select(join => new KeywordNameProjection(
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
            .ToListAsync(cancellationToken);

        return ProjectKeywordNames(rows, maxCount);
    }

    private static List<string> ProjectKeywordNames(
        IReadOnlyList<KeywordNameProjection> rows,
        int maxCount) =>
        rows
            .Select(row => KeywordDiscoverLocalizationSupport.ResolveDisplayName(
                row.RequestedLocaleName,
                row.EnglishLocaleName,
                row.CanonicalName,
                row.Name))
            .Where(name => !string.IsNullOrWhiteSpace(name))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .OrderBy(name => name, StringComparer.OrdinalIgnoreCase)
            .Take(maxCount)
            .ToList();

    private sealed record KeywordNameProjection(
        string Name,
        string? CanonicalName,
        string? RequestedLocaleName,
        string? EnglishLocaleName);
}
