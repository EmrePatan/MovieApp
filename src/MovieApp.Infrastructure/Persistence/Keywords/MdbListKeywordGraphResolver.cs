using System.Globalization;
using Microsoft.EntityFrameworkCore;
using MovieApp.Application.Models.Keywords;
using MovieApp.Application.Services.Keywords;
using MovieApp.Domain.Entities;
using MovieApp.Domain.Enums;

namespace MovieApp.Infrastructure.Persistence.Keywords;

internal static class MdbListKeywordGraphResolver
{
    internal sealed record ResolutionOutcome(
        HashSet<Guid> PromotedKeywordIds,
        MdbListKeywordResolutionStats Stats);

    internal static async Task<ResolutionOutcome> ResolveAsync(
        ApplicationDbContext dbContext,
        IReadOnlyList<MdbListKeywordTransportItem> providerKeywords,
        DateTime syncedAtUtc,
        CancellationToken cancellationToken)
    {
        var providerKeywordCount = providerKeywords.Count;
        if (providerKeywordCount == 0)
        {
            return new ResolutionOutcome(
                [],
                new MdbListKeywordResolutionStats(0, 0, 0, 0, 0, 0));
        }

        var externalIds = providerKeywords
            .Select(keyword => ToExternalId(keyword.ExternalId))
            .Distinct()
            .ToList();

        var existingReferences = await dbContext.KeywordExternalReferences
            .AsNoTracking()
            .Where(reference =>
                reference.Provider == KeywordProvider.MdbList &&
                externalIds.Contains(reference.ExternalId))
            .ToDictionaryAsync(reference => reference.ExternalId, cancellationToken);

        var normalizedNamesNeeded = new HashSet<string>(StringComparer.Ordinal);
        foreach (var keyword in providerKeywords)
        {
            var externalId = ToExternalId(keyword.ExternalId);
            if (existingReferences.ContainsKey(externalId))
            {
                continue;
            }

            var normalized = KeywordCanonicalNormalization.NormalizeKeywordName(keyword.Name);
            if (normalized.Length > 0)
            {
                normalizedNamesNeeded.Add(normalized);
            }
        }

        Dictionary<string, List<Guid>> keywordsByNormalizedName;
        if (normalizedNamesNeeded.Count == 0)
        {
            keywordsByNormalizedName = new Dictionary<string, List<Guid>>(StringComparer.Ordinal);
        }
        else
        {
            var keywordRows = await dbContext.Keywords
                .AsNoTracking()
                .Where(keyword =>
                    keyword.NormalizedName != null &&
                    normalizedNamesNeeded.Contains(keyword.NormalizedName))
                .Select(keyword => new { keyword.Id, keyword.NormalizedName })
                .ToListAsync(cancellationToken);

            keywordsByNormalizedName = keywordRows
                .GroupBy(row => row.NormalizedName!, StringComparer.Ordinal)
                .ToDictionary(
                    group => group.Key,
                    group => group.Select(row => row.Id).ToList(),
                    StringComparer.Ordinal);
        }

        var promotedKeywordIds = new HashSet<Guid>();
        var skippedNoMatch = 0;
        var skippedAmbiguous = 0;
        var referencesCreated = 0;
        var referencesReused = 0;

        foreach (var keyword in providerKeywords)
        {
            if (keyword.ExternalId <= 0 || string.IsNullOrWhiteSpace(keyword.Name))
            {
                skippedNoMatch++;
                continue;
            }

            var externalId = ToExternalId(keyword.ExternalId);
            if (existingReferences.TryGetValue(externalId, out var ownedReference))
            {
                promotedKeywordIds.Add(ownedReference.KeywordId);
                referencesReused++;
                continue;
            }

            var normalized = KeywordCanonicalNormalization.NormalizeKeywordName(keyword.Name);
            if (normalized.Length == 0 ||
                !keywordsByNormalizedName.TryGetValue(normalized, out var matches))
            {
                skippedNoMatch++;
                continue;
            }

            if (matches.Count != 1)
            {
                skippedAmbiguous++;
                continue;
            }

            var canonicalKeywordId = matches[0];
            var ownerKeywordId = await MdbListKeywordExternalReferenceEnsure.EnsureAsync(
                dbContext,
                externalId,
                canonicalKeywordId,
                keyword.Name.Trim(),
                syncedAtUtc,
                cancellationToken);

            promotedKeywordIds.Add(ownerKeywordId);
            existingReferences[externalId] = new KeywordExternalReference
            {
                KeywordId = ownerKeywordId,
                ExternalId = externalId,
            };

            if (ownerKeywordId == canonicalKeywordId)
            {
                referencesCreated++;
            }
            else
            {
                referencesReused++;
            }
        }

        var stats = new MdbListKeywordResolutionStats(
            providerKeywordCount,
            promotedKeywordIds.Count,
            skippedNoMatch,
            skippedAmbiguous,
            referencesCreated,
            referencesReused);

        return new ResolutionOutcome(promotedKeywordIds, stats);
    }

    private static string ToExternalId(int externalId) =>
        externalId.ToString(CultureInfo.InvariantCulture);
}
