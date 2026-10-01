using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using MovieApp.Application.Abstractions.Persistence;
using MovieApp.Application.Configuration;
using MovieApp.Application.Models.Catalog;
using MovieApp.Application.Services.Catalog;
using MovieApp.Application.Services.Keywords;
using MovieApp.Application.Services.Localization;
using MovieApp.Domain.Entities;
using MovieApp.Infrastructure.Persistence;

namespace MovieApp.Infrastructure.Persistence.Repositories;

public sealed class CatalogTitleKeywordReadRepository(
    ApplicationDbContext dbContext,
    IOptions<KeywordDisplayProfileOptions> displayProfileOptions) : ICatalogTitleKeywordReadRepository
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

    private async Task<IReadOnlyList<CatalogKeywordSummary>> LoadLocalizedKeywordsAsync(
        IQueryable<MovieKeyword> movieJoins,
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

        var primaryRows = await movieJoins
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

        var summaries = ProjectKeywordSummaries(primaryRows, maxCount);
        await AppendSupplementalKeywordsAsync(
            movieJoins,
            summaries,
            locale,
            englishLocale,
            maxCount,
            cancellationToken);

        return summaries;
    }

    private async Task<IReadOnlyList<CatalogKeywordSummary>> LoadLocalizedKeywordsAsync(
        IQueryable<TvShowKeyword> tvJoins,
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

        var primaryRows = await tvJoins
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

        var summaries = ProjectKeywordSummaries(primaryRows, maxCount);
        await AppendSupplementalKeywordsAsync(
            tvJoins,
            summaries,
            locale,
            englishLocale,
            maxCount,
            cancellationToken);

        return summaries;
    }

    private async Task AppendSupplementalKeywordsAsync(
        IQueryable<MovieKeyword> movieJoins,
        List<CatalogKeywordSummary> summaries,
        string locale,
        string englishLocale,
        int maxCount,
        CancellationToken cancellationToken)
    {
        var targetCount = Math.Min(maxCount, CatalogDisplayLimits.MinDetailKeywordsTarget);
        if (summaries.Count >= targetCount)
        {
            return;
        }

        var existingIds = summaries.Select(summary => summary.Id).ToList();
        var supplementalRows = await movieJoins
            .Where(join =>
                join.Keyword.DisplayProfile != null &&
                !join.Keyword.DisplayProfile.Displayable &&
                !existingIds.Contains(join.KeywordId))
            .OrderBy(join => join.Keyword.DisplayProfile!.DocumentFrequency)
            .ThenBy(join => join.Keyword.CanonicalName ?? join.Keyword.Name)
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
            .Take(maxCount * 2)
            .ToListAsync(cancellationToken);

        AppendSupplementalSummaries(summaries, supplementalRows, maxCount, targetCount);
    }

    private async Task AppendSupplementalKeywordsAsync(
        IQueryable<TvShowKeyword> tvJoins,
        List<CatalogKeywordSummary> summaries,
        string locale,
        string englishLocale,
        int maxCount,
        CancellationToken cancellationToken)
    {
        var targetCount = Math.Min(maxCount, CatalogDisplayLimits.MinDetailKeywordsTarget);
        if (summaries.Count >= targetCount)
        {
            return;
        }

        var existingIds = summaries.Select(summary => summary.Id).ToList();
        var supplementalRows = await tvJoins
            .Where(join =>
                join.Keyword.DisplayProfile != null &&
                !join.Keyword.DisplayProfile.Displayable &&
                !existingIds.Contains(join.KeywordId))
            .OrderBy(join => join.Keyword.DisplayProfile!.DocumentFrequency)
            .ThenBy(join => join.Keyword.CanonicalName ?? join.Keyword.Name)
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
            .Take(maxCount * 2)
            .ToListAsync(cancellationToken);

        AppendSupplementalSummaries(summaries, supplementalRows, maxCount, targetCount);
    }

    private void AppendSupplementalSummaries(
        List<CatalogKeywordSummary> summaries,
        IReadOnlyList<KeywordRowProjection> supplementalRows,
        int maxCount,
        int targetCount)
    {
        var options = displayProfileOptions.Value;

        foreach (var row in supplementalRows)
        {
            if (summaries.Count >= targetCount || summaries.Count >= maxCount)
            {
                break;
            }

            var canonical = row.CanonicalName ?? row.Name;
            if (!KeywordDisplayQualityEvaluator.IsEligibleForDetailSupplemental(canonical, options))
            {
                continue;
            }

            var name = KeywordDiscoverLocalizationSupport.ResolveDisplayName(
                row.RequestedLocaleName,
                row.EnglishLocaleName,
                row.CanonicalName,
                row.Name);

            if (string.IsNullOrWhiteSpace(name))
            {
                continue;
            }

            if (summaries.Any(summary => summary.Id == row.KeywordId))
            {
                continue;
            }

            summaries.Add(new CatalogKeywordSummary(row.KeywordId, name));
        }
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
