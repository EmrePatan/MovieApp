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

    internal static Task<int> GetTotalCountAsync(
        ApplicationDbContext dbContext,
        string locale,
        string englishLocale,
        bool isEnglishLocale,
        string pattern,
        string normalizedPattern,
        CancellationToken cancellationToken)
    {
        var sql = isEnglishLocale
            ? BuildCountSqlEnglish(locale, englishLocale, pattern, normalizedPattern)
            : BuildCountSqlWithEnglishFallback(locale, englishLocale, pattern, normalizedPattern);

        return dbContext.Database
            .SqlQuery<int>(sql)
            .SingleAsync(cancellationToken);
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
        var sql = isEnglishLocale
            ? BuildRankedPageSqlEnglish(
                locale,
                englishLocale,
                trimmed,
                pattern,
                normalizedPattern,
                hasNormalizedQuery,
                offset,
                limit)
            : BuildRankedPageSqlWithEnglishFallback(
                locale,
                englishLocale,
                trimmed,
                pattern,
                normalizedPattern,
                hasNormalizedQuery,
                offset,
                limit);

        return dbContext.Database
            .SqlQuery<KeywordDiscoverSearchSqlRow>(sql)
            .ToListAsync(cancellationToken);
    }

    private static FormattableString BuildCountSqlWithEnglishFallback(
        string locale,
        string englishLocale,
        string pattern,
        string normalizedPattern) =>
        $"""
                WITH keyword_search_base AS (
                    SELECT
                        k."Id" AS keyword_id,
                        k."Name" AS keyword_name,
                        k."CanonicalName" AS canonical_name,
                        req."Name" AS req_name,
                        req."NormalizedName" AS req_normalized_name,
                        en."Name" AS en_name,
                        en."NormalizedName" AS en_normalized_name
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
                    WHERE k."ClassificationStatus" <> {KeywordClassificationSql.ExcludedStatus}
                ),
                keyword_search_matching AS (
                    SELECT DISTINCT b.keyword_id
                    FROM keyword_search_base AS b
                    WHERE
                        b.keyword_name ILIKE {pattern}
                        OR (b.canonical_name IS NOT NULL AND b.canonical_name ILIKE {pattern})
                        OR (b.req_name IS NOT NULL AND b.req_name ILIKE {pattern})
                        OR (b.req_normalized_name IS NOT NULL AND b.req_normalized_name ILIKE {normalizedPattern})
                        OR (b.en_name IS NOT NULL AND b.en_name ILIKE {pattern})
                        OR (b.en_normalized_name IS NOT NULL AND b.en_normalized_name ILIKE {normalizedPattern})
                )
                SELECT COUNT(*)::int AS "Value"
                FROM keyword_search_matching
                """;

    private static FormattableString BuildCountSqlEnglish(
        string locale,
        string englishLocale,
        string pattern,
        string normalizedPattern) =>
        $"""
            WITH keyword_search_base AS (
                SELECT
                    k."Id" AS keyword_id,
                    k."Name" AS keyword_name,
                    k."CanonicalName" AS canonical_name,
                    req."Name" AS req_name,
                    req."NormalizedName" AS req_normalized_name,
                    en."Name" AS en_name,
                    en."NormalizedName" AS en_normalized_name
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
                WHERE k."ClassificationStatus" <> {KeywordClassificationSql.ExcludedStatus}
            ),
            keyword_search_matching AS (
                SELECT DISTINCT b.keyword_id
                FROM keyword_search_base AS b
                WHERE
                    b.keyword_name ILIKE {pattern}
                    OR (b.canonical_name IS NOT NULL AND b.canonical_name ILIKE {pattern})
                    OR (b.req_name IS NOT NULL AND b.req_name ILIKE {pattern})
                    OR (b.req_normalized_name IS NOT NULL AND b.req_normalized_name ILIKE {normalizedPattern})
            )
            SELECT COUNT(*)::int AS "Value"
            FROM keyword_search_matching
            """;

    private static FormattableString BuildRankedPageSqlWithEnglishFallback(
        string locale,
        string englishLocale,
        string trimmed,
        string pattern,
        string normalizedPattern,
        bool hasNormalizedQuery,
        int offset,
        int limit) =>
        $"""
                WITH keyword_search_base AS (
                    SELECT
                        k."Id" AS keyword_id,
                        k."Name" AS keyword_name,
                        k."CanonicalName" AS canonical_name,
                        req."Name" AS req_name,
                        req."NormalizedName" AS req_normalized_name,
                        en."Name" AS en_name,
                        en."NormalizedName" AS en_normalized_name,
                        COALESCE(
                            NULLIF(BTRIM(req."Name"), ''),
                            NULLIF(BTRIM(en."Name"), ''),
                            NULLIF(BTRIM(k."CanonicalName"), ''),
                            BTRIM(k."Name")) AS display_name
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
                    WHERE k."ClassificationStatus" <> {KeywordClassificationSql.ExcludedStatus}
                ),
                keyword_search_matching AS (
                    SELECT b.*
                    FROM keyword_search_base AS b
                    WHERE
                        b.keyword_name ILIKE {pattern}
                        OR (b.canonical_name IS NOT NULL AND b.canonical_name ILIKE {pattern})
                        OR (b.req_name IS NOT NULL AND b.req_name ILIKE {pattern})
                        OR (b.req_normalized_name IS NOT NULL AND b.req_normalized_name ILIKE {normalizedPattern})
                        OR (b.en_name IS NOT NULL AND b.en_name ILIKE {pattern})
                        OR (b.en_normalized_name IS NOT NULL AND b.en_normalized_name ILIKE {normalizedPattern})
                ),
                ranked AS (
                    SELECT
                        m.keyword_id,
                        m.display_name,
                        CASE
                            WHEN m.display_name ILIKE {trimmed}
                                AND CHAR_LENGTH(m.display_name) = CHAR_LENGTH({trimmed})
                                THEN 0
                            WHEN m.display_name ILIKE {trimmed} || '%'
                                AND NOT (
                                    m.display_name ILIKE {trimmed}
                                    AND CHAR_LENGTH(m.display_name) = CHAR_LENGTH({trimmed}))
                                THEN 1
                            WHEN m.display_name ILIKE {pattern}
                                THEN 2
                            WHEN m.req_name IS NOT NULL
                                AND m.req_name ILIKE {trimmed}
                                AND CHAR_LENGTH(m.req_name) = CHAR_LENGTH({trimmed})
                                THEN 3
                            WHEN m.req_name IS NOT NULL
                                AND m.req_name ILIKE {trimmed} || '%'
                                AND NOT (m.req_name ILIKE {trimmed} AND CHAR_LENGTH(m.req_name) = CHAR_LENGTH({trimmed}))
                                THEN 4
                            WHEN m.req_name IS NOT NULL AND m.req_name ILIKE {pattern}
                                THEN 5
                            WHEN m.canonical_name IS NOT NULL
                                AND m.canonical_name ILIKE {trimmed}
                                AND CHAR_LENGTH(m.canonical_name) = CHAR_LENGTH({trimmed})
                                THEN 6
                            WHEN m.canonical_name IS NOT NULL
                                AND m.canonical_name ILIKE {trimmed} || '%'
                                AND NOT (
                                    m.canonical_name ILIKE {trimmed}
                                    AND CHAR_LENGTH(m.canonical_name) = CHAR_LENGTH({trimmed}))
                                THEN 7
                            WHEN m.canonical_name IS NOT NULL AND m.canonical_name ILIKE {pattern}
                                THEN 8
                            WHEN m.keyword_name ILIKE {trimmed}
                                AND CHAR_LENGTH(m.keyword_name) = CHAR_LENGTH({trimmed})
                                THEN 9
                            WHEN m.keyword_name ILIKE {trimmed} || '%'
                                AND NOT (m.keyword_name ILIKE {trimmed} AND CHAR_LENGTH(m.keyword_name) = CHAR_LENGTH({trimmed}))
                                THEN 10
                            WHEN m.en_name IS NOT NULL
                                AND m.en_name ILIKE {trimmed}
                                AND CHAR_LENGTH(m.en_name) = CHAR_LENGTH({trimmed})
                                THEN 11
                            WHEN m.en_name IS NOT NULL
                                AND m.en_name ILIKE {trimmed} || '%'
                                AND NOT (m.en_name ILIKE {trimmed} AND CHAR_LENGTH(m.en_name) = CHAR_LENGTH({trimmed}))
                                THEN 12
                            WHEN m.en_name IS NOT NULL AND m.en_name ILIKE {pattern}
                                THEN 13
                            WHEN {hasNormalizedQuery}
                                AND m.display_name ILIKE {normalizedPattern}
                                THEN 14
                            ELSE 15
                        END AS rank_score
                    FROM keyword_search_matching AS m
                )
                SELECT ranked.keyword_id AS "KeywordId", ranked.display_name AS "DisplayName"
                FROM ranked
                ORDER BY ranked.rank_score, LOWER(ranked.display_name), ranked.keyword_id
                OFFSET {offset} LIMIT {limit}
                """;

    private static FormattableString BuildRankedPageSqlEnglish(
        string locale,
        string englishLocale,
        string trimmed,
        string pattern,
        string normalizedPattern,
        bool hasNormalizedQuery,
        int offset,
        int limit) =>
        $"""
            WITH keyword_search_base AS (
                SELECT
                    k."Id" AS keyword_id,
                    k."Name" AS keyword_name,
                    k."CanonicalName" AS canonical_name,
                    req."Name" AS req_name,
                    req."NormalizedName" AS req_normalized_name,
                    en."Name" AS en_name,
                    en."NormalizedName" AS en_normalized_name,
                    COALESCE(
                        NULLIF(BTRIM(req."Name"), ''),
                        NULLIF(BTRIM(en."Name"), ''),
                        NULLIF(BTRIM(k."CanonicalName"), ''),
                        BTRIM(k."Name")) AS display_name
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
                WHERE k."ClassificationStatus" <> {KeywordClassificationSql.ExcludedStatus}
            ),
            keyword_search_matching AS (
                SELECT b.*
                FROM keyword_search_base AS b
                WHERE
                    b.keyword_name ILIKE {pattern}
                    OR (b.canonical_name IS NOT NULL AND b.canonical_name ILIKE {pattern})
                    OR (b.req_name IS NOT NULL AND b.req_name ILIKE {pattern})
                    OR (b.req_normalized_name IS NOT NULL AND b.req_normalized_name ILIKE {normalizedPattern})
            ),
            ranked AS (
                SELECT
                    m.keyword_id,
                    m.display_name,
                    CASE
                        WHEN m.display_name ILIKE {trimmed}
                            AND CHAR_LENGTH(m.display_name) = CHAR_LENGTH({trimmed})
                            THEN 0
                        WHEN m.display_name ILIKE {trimmed} || '%'
                            AND NOT (
                                m.display_name ILIKE {trimmed}
                                AND CHAR_LENGTH(m.display_name) = CHAR_LENGTH({trimmed}))
                            THEN 1
                        WHEN m.display_name ILIKE {pattern}
                            THEN 2
                        WHEN m.req_name IS NOT NULL
                            AND m.req_name ILIKE {trimmed}
                            AND CHAR_LENGTH(m.req_name) = CHAR_LENGTH({trimmed})
                            THEN 3
                        WHEN m.req_name IS NOT NULL
                            AND m.req_name ILIKE {trimmed} || '%'
                            AND NOT (m.req_name ILIKE {trimmed} AND CHAR_LENGTH(m.req_name) = CHAR_LENGTH({trimmed}))
                            THEN 4
                        WHEN m.req_name IS NOT NULL AND m.req_name ILIKE {pattern}
                            THEN 5
                        WHEN m.canonical_name IS NOT NULL
                            AND m.canonical_name ILIKE {trimmed}
                            AND CHAR_LENGTH(m.canonical_name) = CHAR_LENGTH({trimmed})
                            THEN 6
                        WHEN m.canonical_name IS NOT NULL
                            AND m.canonical_name ILIKE {trimmed} || '%'
                            AND NOT (
                                m.canonical_name ILIKE {trimmed}
                                AND CHAR_LENGTH(m.canonical_name) = CHAR_LENGTH({trimmed}))
                            THEN 7
                        WHEN m.canonical_name IS NOT NULL AND m.canonical_name ILIKE {pattern}
                            THEN 8
                        WHEN m.keyword_name ILIKE {trimmed}
                            AND CHAR_LENGTH(m.keyword_name) = CHAR_LENGTH({trimmed})
                            THEN 9
                        WHEN m.keyword_name ILIKE {trimmed} || '%'
                            AND NOT (m.keyword_name ILIKE {trimmed} AND CHAR_LENGTH(m.keyword_name) = CHAR_LENGTH({trimmed}))
                            THEN 10
                        WHEN m.en_name IS NOT NULL
                            AND m.en_name ILIKE {trimmed}
                            AND CHAR_LENGTH(m.en_name) = CHAR_LENGTH({trimmed})
                            THEN 11
                        WHEN m.en_name IS NOT NULL
                            AND m.en_name ILIKE {trimmed} || '%'
                            AND NOT (m.en_name ILIKE {trimmed} AND CHAR_LENGTH(m.en_name) = CHAR_LENGTH({trimmed}))
                            THEN 12
                        WHEN m.en_name IS NOT NULL AND m.en_name ILIKE {pattern}
                            THEN 13
                        WHEN {hasNormalizedQuery}
                            AND m.display_name ILIKE {normalizedPattern}
                            THEN 14
                        ELSE 15
                    END AS rank_score
                FROM keyword_search_matching AS m
            )
            SELECT ranked.keyword_id AS "KeywordId", ranked.display_name AS "DisplayName"
            FROM ranked
            ORDER BY ranked.rank_score, LOWER(ranked.display_name), ranked.keyword_id
            OFFSET {offset} LIMIT {limit}
            """;
}
