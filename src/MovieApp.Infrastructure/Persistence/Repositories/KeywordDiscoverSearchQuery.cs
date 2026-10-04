using Microsoft.EntityFrameworkCore;
using MovieApp.Domain.Enums;
using MovieApp.Infrastructure.Persistence.Keywords;

namespace MovieApp.Infrastructure.Persistence.Repositories;

internal static class KeywordDiscoverSearchQuery
{
    private const string TmdbProvider = nameof(KeywordProvider.Tmdb);

    internal sealed class KeywordDiscoverSearchSqlRow
    {
        public Guid KeywordId { get; init; }

        public string DisplayName { get; init; } = string.Empty;
    }

    internal static Task<List<KeywordDiscoverSearchSqlRow>> GetRankedPageAsync(
        ApplicationDbContext dbContext,
        string locale,
        string englishLocale,
        bool isEnglishLocale,
        string trimmed,
        string pattern,
        string normalizedPattern,
        bool hasNormalizedQuery,
        int offset,
        int limit,
        CancellationToken cancellationToken)
    {
        var sql = BuildRankedPageSql(
            locale,
            englishLocale,
            trimmed,
            pattern,
            normalizedPattern,
            hasNormalizedQuery,
            offset,
            limit,
            includeEnglishSearchMatch: !isEnglishLocale);

        return dbContext.Database
            .SqlQuery<KeywordDiscoverSearchSqlRow>(sql)
            .ToListAsync(cancellationToken);
    }

    private static FormattableString BuildRankedPageSql(
        string locale,
        string englishLocale,
        string trimmed,
        string pattern,
        string normalizedPattern,
        bool hasNormalizedQuery,
        int offset,
        int limit,
        bool includeEnglishSearchMatch) =>
        $"""
            SELECT ranked.keyword_id AS "KeywordId", ranked.display_name AS "DisplayName"
            FROM (
                SELECT
                    k."Id" AS keyword_id,
                    COALESCE(
                        NULLIF(BTRIM(req."Name"), ''),
                        NULLIF(BTRIM(en."Name"), ''),
                        NULLIF(BTRIM(k."CanonicalName"), ''),
                        BTRIM(k."Name")) AS display_name,
                    CASE
                        WHEN COALESCE(
                            NULLIF(BTRIM(req."Name"), ''),
                            NULLIF(BTRIM(en."Name"), ''),
                            NULLIF(BTRIM(k."CanonicalName"), ''),
                            BTRIM(k."Name")) ILIKE {trimmed}
                            AND CHAR_LENGTH(COALESCE(
                                NULLIF(BTRIM(req."Name"), ''),
                                NULLIF(BTRIM(en."Name"), ''),
                                NULLIF(BTRIM(k."CanonicalName"), ''),
                                BTRIM(k."Name"))) = CHAR_LENGTH({trimmed})
                            THEN 0
                        WHEN COALESCE(
                            NULLIF(BTRIM(req."Name"), ''),
                            NULLIF(BTRIM(en."Name"), ''),
                            NULLIF(BTRIM(k."CanonicalName"), ''),
                            BTRIM(k."Name")) ILIKE {trimmed} || '%'
                            AND NOT (
                                COALESCE(
                                    NULLIF(BTRIM(req."Name"), ''),
                                    NULLIF(BTRIM(en."Name"), ''),
                                    NULLIF(BTRIM(k."CanonicalName"), ''),
                                    BTRIM(k."Name")) ILIKE {trimmed}
                                AND CHAR_LENGTH(COALESCE(
                                    NULLIF(BTRIM(req."Name"), ''),
                                    NULLIF(BTRIM(en."Name"), ''),
                                    NULLIF(BTRIM(k."CanonicalName"), ''),
                                    BTRIM(k."Name"))) = CHAR_LENGTH({trimmed}))
                            THEN 1
                        WHEN COALESCE(
                            NULLIF(BTRIM(req."Name"), ''),
                            NULLIF(BTRIM(en."Name"), ''),
                            NULLIF(BTRIM(k."CanonicalName"), ''),
                            BTRIM(k."Name")) ILIKE {pattern}
                            THEN 2
                        WHEN req."Name" IS NOT NULL
                            AND req."Name" ILIKE {trimmed}
                            AND CHAR_LENGTH(req."Name") = CHAR_LENGTH({trimmed})
                            THEN 3
                        WHEN req."Name" IS NOT NULL
                            AND req."Name" ILIKE {trimmed} || '%'
                            AND NOT (req."Name" ILIKE {trimmed} AND CHAR_LENGTH(req."Name") = CHAR_LENGTH({trimmed}))
                            THEN 4
                        WHEN req."Name" IS NOT NULL AND req."Name" ILIKE {pattern}
                            THEN 5
                        WHEN k."CanonicalName" IS NOT NULL
                            AND k."CanonicalName" ILIKE {trimmed}
                            AND CHAR_LENGTH(k."CanonicalName") = CHAR_LENGTH({trimmed})
                            THEN 6
                        WHEN k."CanonicalName" IS NOT NULL
                            AND k."CanonicalName" ILIKE {trimmed} || '%'
                            AND NOT (
                                k."CanonicalName" ILIKE {trimmed}
                                AND CHAR_LENGTH(k."CanonicalName") = CHAR_LENGTH({trimmed}))
                            THEN 7
                        WHEN k."CanonicalName" IS NOT NULL AND k."CanonicalName" ILIKE {pattern}
                            THEN 8
                        WHEN k."Name" ILIKE {trimmed}
                            AND CHAR_LENGTH(k."Name") = CHAR_LENGTH({trimmed})
                            THEN 9
                        WHEN k."Name" ILIKE {trimmed} || '%'
                            AND NOT (k."Name" ILIKE {trimmed} AND CHAR_LENGTH(k."Name") = CHAR_LENGTH({trimmed}))
                            THEN 10
                        WHEN en."Name" IS NOT NULL
                            AND en."Name" ILIKE {trimmed}
                            AND CHAR_LENGTH(en."Name") = CHAR_LENGTH({trimmed})
                            THEN 11
                        WHEN en."Name" IS NOT NULL
                            AND en."Name" ILIKE {trimmed} || '%'
                            AND NOT (en."Name" ILIKE {trimmed} AND CHAR_LENGTH(en."Name") = CHAR_LENGTH({trimmed}))
                            THEN 12
                        WHEN en."Name" IS NOT NULL AND en."Name" ILIKE {pattern}
                            THEN 13
                        WHEN {hasNormalizedQuery}
                            AND COALESCE(
                                NULLIF(BTRIM(req."Name"), ''),
                                NULLIF(BTRIM(en."Name"), ''),
                                NULLIF(BTRIM(k."CanonicalName"), ''),
                                BTRIM(k."Name")) ILIKE {normalizedPattern}
                            THEN 14
                        ELSE 15
                    END AS rank_score
                FROM keywords AS k
                INNER JOIN keyword_external_references AS ref
                    ON ref."KeywordId" = k."Id"
                    AND ref."Provider" = {TmdbProvider}
                LEFT JOIN keyword_localizations AS req
                    ON req."KeywordId" = k."Id"
                    AND req."Locale" = {locale}
                LEFT JOIN keyword_localizations AS en
                    ON en."KeywordId" = k."Id"
                    AND en."Locale" = {englishLocale}
                WHERE
                    k."ClassificationStatus" <> {KeywordClassificationSql.ExcludedStatus}
                    AND (
                    k."Name" ILIKE {pattern}
                    OR (k."CanonicalName" IS NOT NULL AND k."CanonicalName" ILIKE {pattern})
                    OR (req."Name" IS NOT NULL AND req."Name" ILIKE {pattern})
                    OR (req."NormalizedName" IS NOT NULL AND req."NormalizedName" ILIKE {normalizedPattern})
                    OR ({includeEnglishSearchMatch} AND en."Name" IS NOT NULL AND en."Name" ILIKE {pattern})
                    OR ({includeEnglishSearchMatch} AND en."NormalizedName" IS NOT NULL AND en."NormalizedName" ILIKE {normalizedPattern})
                    )
            ) AS ranked
            ORDER BY ranked.rank_score, LOWER(ranked.display_name), ranked.keyword_id
            OFFSET {offset} LIMIT {limit}
            """;

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
