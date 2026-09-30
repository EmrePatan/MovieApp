using System.Globalization;
using Microsoft.EntityFrameworkCore;
using MovieApp.Application.Abstractions.Persistence;
using MovieApp.Application.Models.Discovery;
using MovieApp.Application.Models.Movies;
using MovieApp.Domain.Enums;

namespace MovieApp.Infrastructure.Persistence.Repositories;

public sealed class KeywordDiscoverReadRepository(ApplicationDbContext dbContext) : IKeywordDiscoverReadRepository
{
    public async Task<PaginatedResult<KeywordDiscoverItem>> SearchAsync(
        string query,
        int page,
        int pageSize,
        CancellationToken cancellationToken = default)
    {
        var trimmed = query.Trim();
        var pattern = $"%{trimmed}%";

        var baseQuery = dbContext.Keywords
            .AsNoTracking()
            .Where(keyword => keyword.ExternalReferences.Any(reference =>
                reference.Provider == KeywordProvider.Tmdb))
            .Where(keyword =>
                EF.Functions.ILike(keyword.Name, pattern) ||
                (keyword.CanonicalName != null && EF.Functions.ILike(keyword.CanonicalName, pattern)));

        var totalCount = await baseQuery.CountAsync(cancellationToken);

        var items = await baseQuery
            .OrderBy(keyword => keyword.Name)
            .ThenBy(keyword => keyword.Id)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .Select(keyword => new KeywordDiscoverItem(keyword.Id, keyword.Name))
            .ToListAsync(cancellationToken);

        var totalPages = totalCount == 0
            ? 0
            : (int)Math.Ceiling(totalCount / (double)pageSize);

        return new PaginatedResult<KeywordDiscoverItem>(
            items,
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

        var references = await dbContext.KeywordExternalReferences
            .AsNoTracking()
            .Where(reference =>
                reference.Provider == KeywordProvider.Tmdb &&
                distinctIds.Contains(reference.KeywordId))
            .Select(reference => new { reference.KeywordId, reference.ExternalId })
            .ToListAsync(cancellationToken);

        var tmdbIdsByKeywordId = new Dictionary<Guid, int>();
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
}
