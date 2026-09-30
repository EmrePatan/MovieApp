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

        var movieSourcesCreated = await BackfillMovieTmdbSourcesFromJoinsAsync(cancellationToken);
        var tvSourcesCreated = await BackfillTvTmdbSourcesFromJoinsAsync(cancellationToken);

        return await BuildResultAsync(
            conflicts,
            keywordsProcessed,
            canonicalNamesUpdated,
            normalizedNamesUpdated,
            externalReferencesCreated,
            movieSourcesCreated,
            tvSourcesCreated,
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
            movieSourcesCreated: 0,
            tvSourcesCreated: 0,
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

            var expected = keyword.TmdbKeywordId.ToString(CultureInfo.InvariantCulture);
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

        var allKeywordsWithTmdbId = await dbContext.Keywords
            .AsNoTracking()
            .Select(keyword => new { keyword.Id, keyword.TmdbKeywordId })
            .ToListAsync(cancellationToken);

        var refsByExternalId = tmdbRefs.ToLookup(reference => reference.ExternalId);
        foreach (var keyword in allKeywordsWithTmdbId)
        {
            var expected = keyword.TmdbKeywordId.ToString(CultureInfo.InvariantCulture);
            var owners = refsByExternalId[expected].ToList();
            if (owners.Count > 1 && owners.Select(owner => owner.KeywordId).Distinct().Count() > 1)
            {
                conflicts.Add($"TMDB external id {expected} is associated with multiple keyword identities.");
            }
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

            await KeywordGraphTmdbKeywordDualWrite.EnsureTmdbExternalReferencesAsync(
                dbContext,
                batch,
                syncedAtUtc,
                cancellationToken);

            refsCreated += dbContext.ChangeTracker
                .Entries<KeywordExternalReference>()
                .Count(entry => entry.State == EntityState.Added);
            await dbContext.SaveChangesAsync(cancellationToken);
        }

        return (processed, canonicalUpdated, normalizedUpdated, refsCreated);
    }

    private async Task<int> BackfillMovieTmdbSourcesFromJoinsAsync(CancellationToken cancellationToken)
    {
        var backfillTimestampUtc = DateTime.UtcNow;
        if (IsNpgsql())
        {
            return await dbContext.Database.ExecuteSqlInterpolatedAsync(
                $"""
                 INSERT INTO movie_keyword_sources ("MovieId", "KeywordId", "Provider", "FirstSeenAtUtc", "LastSeenAtUtc")
                 SELECT mk."MovieId", mk."KeywordId", 'Tmdb', {backfillTimestampUtc}, {backfillTimestampUtc}
                 FROM movie_keywords mk
                 WHERE NOT EXISTS (
                     SELECT 1
                     FROM movie_keyword_sources mks
                     WHERE mks."MovieId" = mk."MovieId"
                       AND mks."KeywordId" = mk."KeywordId"
                       AND mks."Provider" = 'Tmdb')
                 """,
                cancellationToken);
        }

        var joins = await dbContext.MovieKeywords.AsNoTracking().ToListAsync(cancellationToken);
        var existing = await dbContext.MovieKeywordSources
            .AsNoTracking()
            .Where(source => source.Provider == KeywordProvider.Tmdb)
            .Select(source => new { source.MovieId, source.KeywordId })
            .ToListAsync(cancellationToken);

        var existingSet = existing.Select(pair => (pair.MovieId, pair.KeywordId)).ToHashSet();
        var created = 0;
        foreach (var join in joins)
        {
            if (existingSet.Contains((join.MovieId, join.KeywordId)))
            {
                continue;
            }

            dbContext.MovieKeywordSources.Add(new MovieKeywordSource
            {
                MovieId = join.MovieId,
                KeywordId = join.KeywordId,
                Provider = KeywordProvider.Tmdb,
                FirstSeenAtUtc = backfillTimestampUtc,
                LastSeenAtUtc = backfillTimestampUtc,
            });
            created++;
        }

        await dbContext.SaveChangesAsync(cancellationToken);
        return created;
    }

    private async Task<int> BackfillTvTmdbSourcesFromJoinsAsync(CancellationToken cancellationToken)
    {
        var backfillTimestampUtc = DateTime.UtcNow;
        if (IsNpgsql())
        {
            return await dbContext.Database.ExecuteSqlInterpolatedAsync(
                $"""
                 INSERT INTO tv_show_keyword_sources ("TvShowId", "KeywordId", "Provider", "FirstSeenAtUtc", "LastSeenAtUtc")
                 SELECT tk."TvShowId", tk."KeywordId", 'Tmdb', {backfillTimestampUtc}, {backfillTimestampUtc}
                 FROM tv_show_keywords tk
                 WHERE NOT EXISTS (
                     SELECT 1
                     FROM tv_show_keyword_sources tks
                     WHERE tks."TvShowId" = tk."TvShowId"
                       AND tks."KeywordId" = tk."KeywordId"
                       AND tks."Provider" = 'Tmdb')
                 """,
                cancellationToken);
        }

        var joins = await dbContext.TvShowKeywords.AsNoTracking().ToListAsync(cancellationToken);
        var existing = await dbContext.TvShowKeywordSources
            .AsNoTracking()
            .Where(source => source.Provider == KeywordProvider.Tmdb)
            .Select(source => new { source.TvShowId, source.KeywordId })
            .ToListAsync(cancellationToken);

        var existingSet = existing.Select(pair => (pair.TvShowId, pair.KeywordId)).ToHashSet();
        var created = 0;
        foreach (var join in joins)
        {
            if (existingSet.Contains((join.TvShowId, join.KeywordId)))
            {
                continue;
            }

            dbContext.TvShowKeywordSources.Add(new TvShowKeywordSource
            {
                TvShowId = join.TvShowId,
                KeywordId = join.KeywordId,
                Provider = KeywordProvider.Tmdb,
                FirstSeenAtUtc = backfillTimestampUtc,
                LastSeenAtUtc = backfillTimestampUtc,
            });
            created++;
        }

        await dbContext.SaveChangesAsync(cancellationToken);
        return created;
    }

    private async Task<KeywordGraphReconciliationResult> BuildResultAsync(
        List<string> conflicts,
        int keywordsProcessed,
        int canonicalNamesUpdated,
        int normalizedNamesUpdated,
        int externalReferencesCreated,
        int movieSourcesCreated,
        int tvSourcesCreated,
        CancellationToken cancellationToken)
    {
        var keywordCount = await dbContext.Keywords.CountAsync(cancellationToken);
        var tmdbRefCount = await dbContext.KeywordExternalReferences
            .CountAsync(reference => reference.Provider == KeywordProvider.Tmdb, cancellationToken);

        var missingCanonical = await dbContext.Keywords
            .CountAsync(keyword => keyword.CanonicalName == null || keyword.CanonicalName == string.Empty, cancellationToken);
        var missingNormalized = await dbContext.Keywords
            .CountAsync(keyword => keyword.NormalizedName == null || keyword.NormalizedName == string.Empty, cancellationToken);

        var keywordsWithTmdbId = await dbContext.Keywords.CountAsync(cancellationToken);
        var keywordsWithTmdbRef = await dbContext.KeywordExternalReferences
            .Where(reference => reference.Provider == KeywordProvider.Tmdb)
            .Select(reference => reference.KeywordId)
            .Distinct()
            .CountAsync(cancellationToken);
        var missingTmdbExternalRef = keywordsWithTmdbId - keywordsWithTmdbRef;

        var movieJoinCount = await dbContext.MovieKeywords.CountAsync(cancellationToken);
        var movieTmdbSourceCount = await dbContext.MovieKeywordSources
            .CountAsync(source => source.Provider == KeywordProvider.Tmdb, cancellationToken);
        var missingMovieTmdbSource = await dbContext.MovieKeywords
            .AsNoTracking()
            .Where(join => !dbContext.MovieKeywordSources.Any(source =>
                source.MovieId == join.MovieId &&
                source.KeywordId == join.KeywordId &&
                source.Provider == KeywordProvider.Tmdb))
            .CountAsync(cancellationToken);

        var tvJoinCount = await dbContext.TvShowKeywords.CountAsync(cancellationToken);
        var missingTvTmdbSource = await dbContext.TvShowKeywords
            .AsNoTracking()
            .Where(join => !dbContext.TvShowKeywordSources.Any(source =>
                source.TvShowId == join.TvShowId &&
                source.KeywordId == join.KeywordId &&
                source.Provider == KeywordProvider.Tmdb))
            .CountAsync(cancellationToken);

        var conflictingCount = conflicts.Count;
        var isReady = conflictingCount == 0 &&
                      missingCanonical == 0 &&
                      missingNormalized == 0 &&
                      missingTmdbExternalRef == 0 &&
                      missingMovieTmdbSource == 0 &&
                      missingTvTmdbSource == 0;

        return new KeywordGraphReconciliationResult
        {
            KeywordsProcessed = keywordsProcessed,
            CanonicalNamesUpdated = canonicalNamesUpdated,
            NormalizedNamesUpdated = normalizedNamesUpdated,
            ExternalReferencesCreated = externalReferencesCreated,
            MovieSourcesCreated = movieSourcesCreated,
            TvSourcesCreated = tvSourcesCreated,
            KeywordCount = keywordCount,
            TmdbExternalReferenceCount = tmdbRefCount,
            MissingCanonicalNameCount = missingCanonical,
            MissingNormalizedNameCount = missingNormalized,
            MissingTmdbExternalRefCount = missingTmdbExternalRef,
            MovieKeywordJoinCount = movieJoinCount,
            MissingMovieTmdbSourceCount = missingMovieTmdbSource,
            TvShowKeywordJoinCount = tvJoinCount,
            MissingTvTmdbSourceCount = missingTvTmdbSource,
            ConflictingExternalReferenceCount = conflictingCount,
            Conflicts = conflicts,
            IsReadyForProviderAwareSync = isReady,
        };
    }

    private bool IsNpgsql() =>
        dbContext.Database.IsRelational() &&
        dbContext.Database.ProviderName?.Contains("Npgsql", StringComparison.Ordinal) == true;
}
