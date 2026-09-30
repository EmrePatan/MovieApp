using System.Globalization;
using Microsoft.EntityFrameworkCore;
using MovieApp.Application.Models.Keywords;
using MovieApp.Application.Models.Providers;
using MovieApp.Application.Services.Keywords;
using MovieApp.Domain.Entities;
using MovieApp.Domain.Enums;

namespace MovieApp.Infrastructure.Persistence.Keywords;

internal static class KeywordGraphTmdbKeywordDualWrite
{
    public static void ApplyMetadataAndNameUpdates(
        IReadOnlyDictionary<int, Keyword> keywordsByTmdbId,
        IReadOnlyList<ProviderKeywordSummary> providerKeywords,
        DateTime syncedAtUtc)
    {
        foreach (var summary in providerKeywords)
        {
            if (!keywordsByTmdbId.TryGetValue(summary.TmdbKeywordId, out var keyword))
            {
                continue;
            }

            var nameChanged = !string.Equals(keyword.Name, summary.Name, StringComparison.Ordinal);
            if (nameChanged)
            {
                keyword.Name = summary.Name;
                keyword.UpdatedAt = syncedAtUtc;
            }

            EnsureKeywordMetadata(keyword, summary.Name, syncedAtUtc, forceNormalizedRecompute: nameChanged);
        }
    }

    public static void EnsureKeywordMetadata(
        Keyword keyword,
        string providerName,
        DateTime syncedAtUtc,
        bool forceNormalizedRecompute = false)
    {
        if (string.IsNullOrWhiteSpace(keyword.CanonicalName))
        {
            keyword.CanonicalName = providerName;
            keyword.UpdatedAt = syncedAtUtc;
        }

        var expectedNormalized = KeywordCanonicalNormalization.NormalizeKeywordName(providerName);
        if (forceNormalizedRecompute ||
            string.IsNullOrWhiteSpace(keyword.NormalizedName) ||
            !string.Equals(keyword.NormalizedName, expectedNormalized, StringComparison.Ordinal))
        {
            keyword.NormalizedName = expectedNormalized;
            keyword.UpdatedAt = syncedAtUtc;
        }
    }

    public static void PrepareNewKeywordEntity(Keyword keyword, ProviderKeywordSummary summary, DateTime syncedAtUtc)
    {
        keyword.TmdbKeywordId = summary.TmdbKeywordId;
        keyword.Name = summary.Name;
        keyword.SemanticCategory = KeywordSemanticCategory.Unknown;
        keyword.ClassificationStatus = KeywordClassificationStatus.Auto;
        keyword.CreatedAt = syncedAtUtc;
        keyword.UpdatedAt = syncedAtUtc;
        EnsureKeywordMetadata(keyword, summary.Name, syncedAtUtc, forceNormalizedRecompute: true);

        keyword.ExternalReferences.Add(new KeywordExternalReference
        {
            KeywordId = keyword.Id,
            Provider = KeywordProvider.Tmdb,
            ExternalId = summary.TmdbKeywordId.ToString(CultureInfo.InvariantCulture),
            ExternalName = summary.Name,
            CreatedAt = syncedAtUtc,
        });
    }

    public static async Task EnsureTmdbExternalReferencesAsync(
        ApplicationDbContext dbContext,
        IReadOnlyCollection<Keyword> keywords,
        DateTime syncedAtUtc,
        CancellationToken cancellationToken)
    {
        if (keywords.Count == 0)
        {
            return;
        }

        var keywordIds = keywords.Select(keyword => keyword.Id).ToList();
        var existingRefs = await dbContext.KeywordExternalReferences
            .AsNoTracking()
            .Where(reference => reference.Provider == KeywordProvider.Tmdb && keywordIds.Contains(reference.KeywordId))
            .ToListAsync(cancellationToken);

        var refsByKeywordId = existingRefs
            .GroupBy(reference => reference.KeywordId)
            .ToDictionary(group => group.Key, group => group.ToList());

        foreach (var keyword in keywords)
        {
            var expectedExternalId = keyword.TmdbKeywordId.ToString(CultureInfo.InvariantCulture);
            if (keyword.ExternalReferences.Any(reference =>
                    reference.Provider == KeywordProvider.Tmdb &&
                    string.Equals(reference.ExternalId, expectedExternalId, StringComparison.Ordinal)))
            {
                continue;
            }

            if (refsByKeywordId.TryGetValue(keyword.Id, out var refsForKeyword))
            {
                var mismatched = refsForKeyword.FirstOrDefault(reference =>
                    !string.Equals(reference.ExternalId, expectedExternalId, StringComparison.Ordinal));
                if (mismatched is not null)
                {
                    throw new KeywordGraphDataIntegrityException(
                        $"Keyword {keyword.Id} has TMDB external reference ExternalId={mismatched.ExternalId} but TmdbKeywordId={keyword.TmdbKeywordId}.");
                }

                if (refsForKeyword.Count > 0)
                {
                    continue;
                }
            }

            var owner = await dbContext.KeywordExternalReferences
                .AsNoTracking()
                .FirstOrDefaultAsync(
                    reference => reference.Provider == KeywordProvider.Tmdb &&
                                 reference.ExternalId == expectedExternalId,
                    cancellationToken);
            if (owner is not null && owner.KeywordId != keyword.Id)
            {
                throw new KeywordGraphDataIntegrityException(
                    $"TMDB external id {expectedExternalId} is owned by keyword {owner.KeywordId} but TmdbKeywordId {keyword.TmdbKeywordId} resolves to keyword {keyword.Id}.");
            }

            dbContext.KeywordExternalReferences.Add(new KeywordExternalReference
            {
                KeywordId = keyword.Id,
                Provider = KeywordProvider.Tmdb,
                ExternalId = expectedExternalId,
                ExternalName = keyword.Name,
                CreatedAt = syncedAtUtc,
            });
        }
    }

    public static async Task InsertMissingKeywordsWithDualWriteAsync(
        ApplicationDbContext dbContext,
        IReadOnlyList<ProviderKeywordSummary> keywords,
        DateTime syncedAtUtc,
        CancellationToken cancellationToken)
    {
        foreach (var summary in keywords)
        {
            var keywordId = Guid.NewGuid();
            var canonicalName = summary.Name;
            var normalizedName = KeywordCanonicalNormalization.NormalizeKeywordName(summary.Name);

            await dbContext.Database.ExecuteSqlInterpolatedAsync(
                $"""
                 INSERT INTO keywords ("Id", "TmdbKeywordId", "Name", "CanonicalName", "NormalizedName", "SemanticCategory", "ClassificationStatus", "CreatedAt", "UpdatedAt")
                 VALUES ({keywordId}, {summary.TmdbKeywordId}, {summary.Name}, {canonicalName}, {normalizedName}, {(int)KeywordSemanticCategory.Unknown}, {(int)KeywordClassificationStatus.Auto}, {syncedAtUtc}, {syncedAtUtc})
                 ON CONFLICT ("TmdbKeywordId") DO NOTHING
                 """,
                cancellationToken);
        }

        var tmdbIds = keywords.Select(keyword => keyword.TmdbKeywordId).Distinct().ToList();
        var insertedKeywords = await dbContext.Keywords
            .AsNoTracking()
            .Where(keyword => tmdbIds.Contains(keyword.TmdbKeywordId))
            .ToListAsync(cancellationToken);

        foreach (var keyword in insertedKeywords)
        {
            var externalId = keyword.TmdbKeywordId.ToString(CultureInfo.InvariantCulture);
            await dbContext.Database.ExecuteSqlInterpolatedAsync(
                $"""
                 INSERT INTO keyword_external_references ("Provider", "ExternalId", "KeywordId", "ExternalName", "CreatedAt")
                 VALUES ({KeywordProvider.Tmdb.ToString()}, {externalId}, {keyword.Id}, {keyword.Name}, {syncedAtUtc})
                 ON CONFLICT ("Provider", "ExternalId") DO NOTHING
                 """,
                cancellationToken);

            await AssertTmdbExternalReferenceOwnershipAsync(dbContext, keyword, cancellationToken);
        }
    }

    public static async Task AssertTmdbExternalReferenceOwnershipAsync(
        ApplicationDbContext dbContext,
        Keyword keyword,
        CancellationToken cancellationToken)
    {
        var expectedExternalId = keyword.TmdbKeywordId.ToString(CultureInfo.InvariantCulture);
        var owner = await dbContext.KeywordExternalReferences
            .AsNoTracking()
            .FirstOrDefaultAsync(
                reference => reference.Provider == KeywordProvider.Tmdb &&
                             reference.ExternalId == expectedExternalId,
                cancellationToken);

        if (owner is null)
        {
            return;
        }

        if (owner.KeywordId != keyword.Id)
        {
            throw new KeywordGraphDataIntegrityException(
                $"TMDB external id {expectedExternalId} is owned by keyword {owner.KeywordId} but TmdbKeywordId {keyword.TmdbKeywordId} resolves to keyword {keyword.Id}.");
        }
    }
}
