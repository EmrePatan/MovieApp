using Microsoft.EntityFrameworkCore;
using MovieApp.Application.Abstractions.Persistence;
using MovieApp.Application.Services.Keywords;
using MovieApp.Application.Services.Localization;
using MovieApp.Domain.Entities;
using MovieApp.Domain.Enums;

namespace MovieApp.Infrastructure.Persistence.Repositories;

public sealed class KeywordLocalizationCoverageRepository(ApplicationDbContext dbContext)
    : IKeywordLocalizationCoverageRepository
{
    public Task<int> GetEligibleKeywordCountAsync(CancellationToken cancellationToken = default) =>
        dbContext.Keywords.AsNoTracking().CountAsync(cancellationToken);

    public async Task<IReadOnlyList<KeywordLocalizationLocaleCoverage>> GetLocaleCoverageAsync(
        CancellationToken cancellationToken = default)
    {
        var eligible = await GetEligibleKeywordCountAsync(cancellationToken);
        var aggregateRows = await dbContext.KeywordLocalizations
            .AsNoTracking()
            .Where(localization =>
                SupportedContentLocales.KeywordBulkTranslationTargetLocales.Contains(localization.Locale))
            .GroupBy(localization => localization.Locale)
            .Select(group => new
            {
                Locale = group.Key,
                Localized = group.Count(),
                MachineUnreviewed = group.Count(localization =>
                    localization.TranslationSource == KeywordTranslationSource.Machine &&
                    localization.ReviewStatus == KeywordTranslationReviewStatus.Unreviewed),
                Reviewed = group.Count(localization =>
                    localization.ReviewStatus == KeywordTranslationReviewStatus.Reviewed),
                Curated = group.Count(localization =>
                    localization.TranslationSource == KeywordTranslationSource.Curated),
            })
            .ToListAsync(cancellationToken);

        var staleByLocale = await ComputeStaleCountsByLocaleAsync(cancellationToken);
        var aggregateByLocale = aggregateRows.ToDictionary(row => row.Locale, StringComparer.OrdinalIgnoreCase);

        var results = new List<KeywordLocalizationLocaleCoverage>();
        foreach (var locale in SupportedContentLocales.KeywordBulkTranslationTargetLocales)
        {
            aggregateByLocale.TryGetValue(locale, out var aggregate);
            var localized = aggregate?.Localized ?? 0;
            var missing = Math.Max(0, eligible - localized);
            var coveragePercent = eligible == 0
                ? 0m
                : Math.Round(localized * 100m / eligible, 2, MidpointRounding.AwayFromZero);
            staleByLocale.TryGetValue(locale, out var stale);

            results.Add(new KeywordLocalizationLocaleCoverage(
                locale,
                eligible,
                localized,
                coveragePercent,
                missing,
                aggregate?.MachineUnreviewed ?? 0,
                aggregate?.Reviewed ?? 0,
                aggregate?.Curated ?? 0,
                stale));
        }

        return results;
    }

    private async Task<Dictionary<string, int>> ComputeStaleCountsByLocaleAsync(
        CancellationToken cancellationToken)
    {
        var rows = await (
            from localization in dbContext.KeywordLocalizations.AsNoTracking()
            join keyword in dbContext.Keywords.AsNoTracking()
                on localization.KeywordId equals keyword.Id
            where SupportedContentLocales.KeywordBulkTranslationTargetLocales.Contains(localization.Locale)
            select new
            {
                localization.Locale,
                localization.SourceTextHash,
                keyword.Name,
                keyword.CanonicalName,
            })
            .ToListAsync(cancellationToken);

        var staleByLocale = SupportedContentLocales.KeywordBulkTranslationTargetLocales
            .ToDictionary(locale => locale, _ => 0, StringComparer.OrdinalIgnoreCase);

        foreach (var row in rows)
        {
            var sourceText = KeywordLocalizationSourceText.ResolveSourceText(new Keyword
            {
                Name = row.Name,
                CanonicalName = row.CanonicalName,
            });
            var hash = KeywordLocalizationSourceText.ComputeSourceTextHash(
                KeywordLocalizationSourceText.NormalizeSourceTextForHash(sourceText));
            if (!string.Equals(row.SourceTextHash, hash, StringComparison.Ordinal))
            {
                staleByLocale[row.Locale]++;
            }
        }

        return staleByLocale;
    }
}
