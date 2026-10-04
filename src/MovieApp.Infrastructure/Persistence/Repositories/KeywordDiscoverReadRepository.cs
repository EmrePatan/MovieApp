using System.Globalization;
using Microsoft.EntityFrameworkCore;
using MovieApp.Application.Abstractions.Persistence;
using MovieApp.Application.Models.Discovery;
using MovieApp.Application.Models.Movies;
using MovieApp.Application.Services.Keywords;
using MovieApp.Application.Services.Localization;
using MovieApp.Domain.Enums;

namespace MovieApp.Infrastructure.Persistence.Repositories;

public sealed class KeywordDiscoverReadRepository(ApplicationDbContext dbContext) : IKeywordDiscoverReadRepository
{
    public async Task<PaginatedResult<KeywordDiscoverItem>> SearchAsync(
        string query,
        string contentLocale,
        int page,
        int pageSize,
        CancellationToken cancellationToken = default)
    {
        var locale = KeywordDiscoverLocalizationSupport.NormalizeLocale(contentLocale);
        var englishLocale = SupportedContentLocales.EnglishUnitedStates;
        var isEnglishLocale = string.Equals(locale, englishLocale, StringComparison.OrdinalIgnoreCase);

        var trimmed = query.Trim();
        var pattern = $"%{trimmed}%";
        var normalizedQuery = KeywordDiscoverLocalizationSupport.NormalizeSearchName(trimmed);
        var normalizedPattern = string.IsNullOrEmpty(normalizedQuery) ? pattern : $"%{normalizedQuery}%";
        var hasNormalizedQuery = normalizedQuery.Length > 0;

        var totalCount = await KeywordDiscoverSearchQuery.BuildMatchingKeywordIds(
                dbContext,
                locale,
                englishLocale,
                isEnglishLocale,
                pattern,
                normalizedPattern)
            .CountAsync(cancellationToken);

        if (totalCount == 0)
        {
            return new PaginatedResult<KeywordDiscoverItem>(
                [],
                page,
                pageSize,
                0,
                0);
        }

        var offset = (page - 1) * pageSize;
        var pageRows = await KeywordDiscoverSearchQuery.GetRankedPageAsync(
            dbContext,
            locale,
            englishLocale,
            isEnglishLocale,
            trimmed,
            pattern,
            normalizedPattern,
            hasNormalizedQuery,
            offset,
            pageSize,
            cancellationToken);

        var pageItems = pageRows
            .Select(row => new KeywordDiscoverItem(row.KeywordId, row.DisplayName))
            .ToList();

        var totalPages = (int)Math.Ceiling(totalCount / (double)pageSize);

        return new PaginatedResult<KeywordDiscoverItem>(
            pageItems,
            page,
            pageSize,
            totalCount,
            totalPages);
    }

    public async Task<IReadOnlyList<int>> ResolveTmdbKeywordIdsAsync(
        IReadOnlyList<Guid> keywordIds,
        CancellationToken cancellationToken = default)
    {
        if (keywordIds.Count == 0)
        {
            return [];
        }

        var distinctIds = keywordIds
            .Where(id => id != Guid.Empty)
            .Distinct()
            .ToList();

        if (distinctIds.Count == 0)
        {
            return [];
        }

        var tmdbIdsByKeywordId = await LoadTmdbKeywordIdsAsync(distinctIds, cancellationToken);

        var resolved = new List<int>();
        var seen = new HashSet<int>();
        foreach (var keywordId in keywordIds)
        {
            if (!tmdbIdsByKeywordId.TryGetValue(keywordId, out var tmdbKeywordId))
            {
                continue;
            }

            if (seen.Add(tmdbKeywordId))
            {
                resolved.Add(tmdbKeywordId);
            }
        }

        return resolved;
    }

    private async Task<Dictionary<Guid, int>> LoadTmdbKeywordIdsAsync(
        IReadOnlyList<Guid> keywordIds,
        CancellationToken cancellationToken)
    {
        var tmdbIdsByKeywordId = new Dictionary<Guid, int>();

        var references = await dbContext.KeywordExternalReferences
            .AsNoTracking()
            .Where(reference =>
                reference.Provider == KeywordProvider.Tmdb &&
                keywordIds.Contains(reference.KeywordId) &&
                reference.Keyword.ClassificationStatus != KeywordClassificationStatus.Excluded)
            .Select(reference => new { reference.KeywordId, reference.ExternalId })
            .ToListAsync(cancellationToken);

        foreach (var reference in references)
        {
            if (!int.TryParse(
                    reference.ExternalId,
                    NumberStyles.Integer,
                    CultureInfo.InvariantCulture,
                    out var tmdbKeywordId) ||
                tmdbKeywordId <= 0)
            {
                continue;
            }

            tmdbIdsByKeywordId.TryAdd(reference.KeywordId, tmdbKeywordId);
        }

        var unresolvedIds = keywordIds.Where(id => !tmdbIdsByKeywordId.ContainsKey(id)).ToList();
        if (unresolvedIds.Count == 0)
        {
            return tmdbIdsByKeywordId;
        }

        var columnMappings = await dbContext.Keywords
            .AsNoTracking()
            .Where(keyword =>
                unresolvedIds.Contains(keyword.Id) &&
                keyword.ClassificationStatus != KeywordClassificationStatus.Excluded &&
                keyword.TmdbKeywordId != null &&
                keyword.TmdbKeywordId > 0)
            .Select(keyword => new { keyword.Id, keyword.TmdbKeywordId })
            .ToListAsync(cancellationToken);

        foreach (var mapping in columnMappings)
        {
            if (mapping.TmdbKeywordId is int tmdbKeywordId && tmdbKeywordId > 0)
            {
                tmdbIdsByKeywordId.TryAdd(mapping.Id, tmdbKeywordId);
            }
        }

        return tmdbIdsByKeywordId;
    }
}
