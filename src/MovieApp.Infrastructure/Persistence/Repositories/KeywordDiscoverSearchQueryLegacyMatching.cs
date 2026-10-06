using Microsoft.EntityFrameworkCore;
using MovieApp.Domain.Enums;

namespace MovieApp.Infrastructure.Persistence.Repositories;

/// <summary>
/// Pre-MADDE-2 EF matching query retained for PostgreSQL equivalence tests only.
/// </summary>
internal static class KeywordDiscoverSearchQueryLegacyMatching
{
    internal static IQueryable<Guid> BuildMatchingKeywordIds(
        ApplicationDbContext dbContext,
        string locale,
        string englishLocale,
        bool isEnglishLocale,
        string pattern,
        string normalizedPattern)
    {
        var tmdbKeywords = dbContext.Keywords
            .AsNoTracking()
            .Where(keyword =>
                keyword.ClassificationStatus != KeywordClassificationStatus.Excluded &&
                keyword.ExternalReferences.Any(reference =>
                    reference.Provider == KeywordProvider.Tmdb));

        var matchingKeywords = isEnglishLocale
            ? tmdbKeywords.Where(keyword =>
                EF.Functions.ILike(keyword.Name, pattern) ||
                (keyword.CanonicalName != null && EF.Functions.ILike(keyword.CanonicalName, pattern)) ||
                keyword.Localizations.Any(localization =>
                    localization.Locale == locale &&
                    (EF.Functions.ILike(localization.Name, pattern) ||
                     EF.Functions.ILike(localization.NormalizedName, normalizedPattern))))
            : tmdbKeywords.Where(keyword =>
                EF.Functions.ILike(keyword.Name, pattern) ||
                (keyword.CanonicalName != null && EF.Functions.ILike(keyword.CanonicalName, pattern)) ||
                keyword.Localizations.Any(localization =>
                    localization.Locale == locale &&
                    (EF.Functions.ILike(localization.Name, pattern) ||
                     EF.Functions.ILike(localization.NormalizedName, normalizedPattern))) ||
                keyword.Localizations.Any(localization =>
                    localization.Locale == englishLocale &&
                    (EF.Functions.ILike(localization.Name, pattern) ||
                     EF.Functions.ILike(localization.NormalizedName, normalizedPattern))));

        return matchingKeywords.Select(keyword => keyword.Id);
    }
}
