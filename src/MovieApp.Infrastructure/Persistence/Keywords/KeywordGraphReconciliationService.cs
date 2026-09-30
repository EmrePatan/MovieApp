using System.Globalization;
using Microsoft.EntityFrameworkCore;
using MovieApp.Application.Abstractions.Keywords;
using MovieApp.Application.Models.Keywords;
using MovieApp.Application.Services.Keywords;
using MovieApp.Domain.Entities;
using MovieApp.Domain.Enums;

namespace MovieApp.Infrastructure.Persistence.Keywords;

public sealed class KeywordGraphReconciliationService(ApplicationDbContext dbContext) : IKeywordGraphReconciliationService
{
    private const int KeywordBatchSize = 500;

    public async Task<KeywordGraphReconciliationResult> ReconcileAsync(CancellationToken cancellationToken = default)
    {
        var conflicts = await DetectExternalReferenceConflictsAsync(cancellationToken);
        var canonicalNamesUpdated = 0;
        var normalizedNamesUpdated = 0;
        var keywordsProcessed = 0;
        var externalReferencesCreated = 0;

        if (conflicts.Count == 0)
        {
            (keywordsProcessed, canonicalNamesUpdated, normalizedNamesUpdated, externalReferencesCreated) =
                await ReconcileKeywordMetadataAndReferencesAsync(cancellationToken);
        }

        return await BuildResultAsync(
            conflicts,
            keywordsProcessed,
            canonicalNamesUpdated,
            normalizedNamesUpdated,
            externalReferencesCreated,
            cancellationToken);
    }

    public async Task<KeywordGraphReconciliationResult> VerifyReadinessAsync(CancellationToken cancellationToken = default)
    {
        var conflicts = await DetectExternalReferenceConflictsAsync(cancellationToken);
        return await BuildResultAsync(
            conflicts,
            keywordsProcessed: 0,
            canonicalNamesUpdated: 0,
            normalizedNamesUpdated: 0,
            externalReferencesCreated: 0,
            cancellationToken);
    }

    private async Task<List<string>> DetectExternalReferenceConflictsAsync(CancellationToken cancellationToken)
    {
        var conflicts = new List<string>();

        var tmdbRefs = await dbContext.KeywordExternalReferences
            .AsNoTracking()
            .Where(reference => reference.Provider == KeywordProvider.Tmdb)
            .Select(reference => new { reference.KeywordId, reference.ExternalId })
            .ToListAsync(cancellationToken);

        var keywordIds = tmdbRefs.Select(reference => reference.KeywordId).Distinct().ToList();
        var keywords = await dbContext.Keywords
            .AsNoTracking()
            .Where(keyword => keywordIds.Contains(keyword.Id))
            .Select(keyword => new { keyword.Id, keyword.TmdbKeywordId })
            .ToListAsync(cancellationToken);

        var keywordsById = keywords.ToDictionary(keyword => keyword.Id);

        foreach (var reference in tmdbRefs)
        {
            if (!keywordsById.TryGetValue(reference.KeywordId, out var keyword))
            {
                conflicts.Add($"TMDB external reference {reference.ExternalId} points to missing keyword {reference.KeywordId}.");
                continue;
            }

            if (keyword.TmdbKeywordId is null)
            {
                conflicts.Add(
                    $"Keyword {keyword.Id} has TMDB external reference ExternalId={reference.ExternalId} but TmdbKeywordId is null.");
                continue;
            }

            var expected = keyword.TmdbKeywordId.Value.ToString(CultureInfo.InvariantCulture);
            if (!string.Equals(reference.ExternalId, expected, StringComparison.Ordinal))
            {
                conflicts.Add(
                    $"Keyword {keyword.Id} TmdbKeywordId={keyword.TmdbKeywordId} conflicts with external reference ExternalId={reference.ExternalId}.");
            }
        }

        var refsByKeyword = tmdbRefs.GroupBy(reference => reference.KeywordId);
        foreach (var group in refsByKeyword.Where(g => g.Count() > 1))
        {
            conflicts.Add($"Keyword {group.Key} has multiple TMDB external references.");
        }

        var tmdbBackedKeywords = await dbContext.Keywords
            .AsNoTracking()
            .Where(keyword => keyword.TmdbKeywordId != null)
            .Select(keyword => new { keyword.Id, keyword.TmdbKeywordId })
            .ToListAsync(cancellationToken);

        var refsByExternalId = tmdbRefs.ToLookup(reference => reference.ExternalId);
        foreach (var keyword in tmdbBackedKeywords)
        {
            var expected = keyword.TmdbKeywordId!.Value.ToString(CultureInfo.InvariantCulture);
            var owners = refsByExternalId[expected].ToList();
            if (owners.Count > 1 && owners.Select(owner => owner.KeywordId).Distinct().Count() > 1)
            {
                conflicts.Add($"TMDB external id {expected} is associated with multiple keyword identities.");
            }
        }

        var allExternalReferences = await dbContext.KeywordExternalReferences
            .AsNoTracking()
            .Select(reference => new { reference.Provider, reference.ExternalId, reference.KeywordId })
            .ToListAsync(cancellationToken);

        foreach (var shared in allExternalReferences
                     .GroupBy(reference => (reference.Provider, reference.ExternalId))
                     .Where(group => group.Select(reference => reference.KeywordId).Distinct().Count() > 1))
        {
            var sample = shared.First();
            conflicts.Add(
                $"External id {sample.ExternalId} for provider {sample.Provider} is associated with multiple keyword identities.");
        }

        return conflicts;
    }

    private async Task<(int Processed, int CanonicalUpdated, int NormalizedUpdated, int RefsCreated)> ReconcileKeywordMetadataAndReferencesAsync(
        CancellationToken cancellationToken)
    {
        var canonicalUpdated = 0;
        var normalizedUpdated = 0;
        var refsCreated = 0;
        var processed = 0;
        var syncedAtUtc = DateTime.UtcNow;

        var totalKeywords = await dbContext.Keywords.CountAsync(cancellationToken);
        for (var offset = 0; offset < totalKeywords; offset += KeywordBatchSize)
        {
            var batch = await dbContext.Keywords
                .OrderBy(keyword => keyword.Id)
                .Skip(offset)
                .Take(KeywordBatchSize)
                .ToListAsync(cancellationToken);

            foreach (var keyword in batch)
            {
                processed++;
                if (string.IsNullOrWhiteSpace(keyword.CanonicalName))
                {
                    keyword.CanonicalName = keyword.Name;
                    keyword.UpdatedAt = syncedAtUtc;
                    canonicalUpdated++;
                }

                var expectedNormalized = KeywordCanonicalNormalization.NormalizeKeywordName(keyword.Name);
                if (string.IsNullOrWhiteSpace(keyword.NormalizedName) ||
                    !string.Equals(keyword.NormalizedName, expectedNormalized, StringComparison.Ordinal))
                {
                    keyword.NormalizedName = expectedNormalized;
                    keyword.UpdatedAt = syncedAtUtc;
                    normalizedUpdated++;
                }
            }

            var tmdbBackedKeywords = batch.Where(keyword => keyword.TmdbKeywordId.HasValue).ToList();
            await KeywordGraphTmdbKeywordDualWrite.EnsureTmdbExternalReferencesAsync(
                dbContext,
                tmdbBackedKeywords,
                syncedAtUtc,
                cancellationToken);

            refsCreated += dbContext.ChangeTracker
                .Entries<KeywordExternalReference>()
                .Count(entry => entry.State == EntityState.Added);
            await dbContext.SaveChangesAsync(cancellationToken);
        }

        return (processed, canonicalUpdated, normalizedUpdated, refsCreated);
    }

    private async Task<KeywordGraphReconciliationResult> BuildResultAsync(
        List<string> conflicts,
        int keywordsProcessed,
        int canonicalNamesUpdated,
        int normalizedNamesUpdated,
        int externalReferencesCreated,
        CancellationToken cancellationToken)
    {
        var keywordCount = await dbContext.Keywords.CountAsync(cancellationToken);
        var tmdbRefCount = await dbContext.KeywordExternalReferences
            .CountAsync(reference => reference.Provider == KeywordProvider.Tmdb, cancellationToken);

        var missingCanonical = await dbContext.Keywords
            .CountAsync(keyword => keyword.CanonicalName == null || keyword.CanonicalName == string.Empty, cancellationToken);
        var missingNormalized = await dbContext.Keywords
            .CountAsync(keyword => keyword.NormalizedName == null || keyword.NormalizedName == string.Empty, cancellationToken);

        var missingTmdbExternalRef = await dbContext.Keywords
            .CountAsync(
                keyword => keyword.TmdbKeywordId != null &&
                           !dbContext.KeywordExternalReferences.Any(reference =>
                               reference.KeywordId == keyword.Id &&
                               reference.Provider == KeywordProvider.Tmdb),
                cancellationToken);

        var movieJoinCount = await dbContext.MovieKeywords.CountAsync(cancellationToken);
        var missingMovieKeywordSource = await dbContext.MovieKeywords
            .AsNoTracking()
            .Where(join => !dbContext.MovieKeywordSources.Any(source =>
                source.MovieId == join.MovieId &&
                source.KeywordId == join.KeywordId))
            .CountAsync(cancellationToken);

        var tvJoinCount = await dbContext.TvShowKeywords.CountAsync(cancellationToken);
        var missingTvKeywordSource = await dbContext.TvShowKeywords
            .AsNoTracking()
            .Where(join => !dbContext.TvShowKeywordSources.Any(source =>
                source.TvShowId == join.TvShowId &&
                source.KeywordId == join.KeywordId))
            .CountAsync(cancellationToken);

        var missingMovieMaterializedJoin = await dbContext.MovieKeywordSources
            .AsNoTracking()
            .GroupBy(source => new { source.MovieId, source.KeywordId })
            .Where(group => !dbContext.MovieKeywords.Any(join =>
                join.MovieId == group.Key.MovieId &&
                join.KeywordId == group.Key.KeywordId))
            .CountAsync(cancellationToken);

        var missingTvMaterializedJoin = await dbContext.TvShowKeywordSources
            .AsNoTracking()
            .GroupBy(source => new { source.TvShowId, source.KeywordId })
            .Where(group => !dbContext.TvShowKeywords.Any(join =>
                join.TvShowId == group.Key.TvShowId &&
                join.KeywordId == group.Key.KeywordId))
            .CountAsync(cancellationToken);

        var conflictingCount = conflicts.Count;
        var isReady = conflictingCount == 0 &&
                      missingCanonical == 0 &&
                      missingNormalized == 0 &&
                      missingTmdbExternalRef == 0 &&
                      missingMovieKeywordSource == 0 &&
                      missingTvKeywordSource == 0 &&
                      missingMovieMaterializedJoin == 0 &&
                      missingTvMaterializedJoin == 0;

        return new KeywordGraphReconciliationResult
        {
            KeywordsProcessed = keywordsProcessed,
            CanonicalNamesUpdated = canonicalNamesUpdated,
            NormalizedNamesUpdated = normalizedNamesUpdated,
            ExternalReferencesCreated = externalReferencesCreated,
            KeywordCount = keywordCount,
            TmdbExternalReferenceCount = tmdbRefCount,
            MissingCanonicalNameCount = missingCanonical,
            MissingNormalizedNameCount = missingNormalized,
            MissingTmdbExternalRefCount = missingTmdbExternalRef,
            MovieKeywordJoinCount = movieJoinCount,
            MissingMovieKeywordSourceCount = missingMovieKeywordSource,
            MissingMovieMaterializedJoinCount = missingMovieMaterializedJoin,
            TvShowKeywordJoinCount = tvJoinCount,
            MissingTvShowKeywordSourceCount = missingTvKeywordSource,
            MissingTvShowMaterializedJoinCount = missingTvMaterializedJoin,
            ConflictingExternalReferenceCount = conflictingCount,
            Conflicts = conflicts,
            IsReadyForProviderAwareSync = isReady,
        };
    }
}
