using Microsoft.EntityFrameworkCore;
using MovieApp.Application.Services.Keywords;
using MovieApp.Application.Services.Localization;
using MovieApp.Domain.Entities;
using MovieApp.Domain.Enums;
using MovieApp.Infrastructure.Persistence;
using MovieApp.Infrastructure.Persistence.Repositories;

namespace MovieApp.IntegrationTests.Persistence;

[Collection("CatalogPersistence")]
public sealed class KeywordLocalizationBackfillPostgreSqlIntegrationTests
{
    [Fact]
    public async Task SelectCandidatesBoundedRunsProcessAllMissingWithoutReselectingCurrent()
    {
        await using var context = CatalogPersistenceFixture.CreateContext();
        const int keywordCount = 60;
        const int maxItems = 15;
        var locale = SupportedContentLocales.TurkishTurkey;
        var now = DateTime.UtcNow;
        var existingKeywordIds = await context.Keywords.AsNoTracking().Select(keyword => keyword.Id).ToListAsync();
        await EnsureCurrentMachineLocalizationsForKeywordsAsync(context, existingKeywordIds, locale, now);

        var keywordIds = Enumerable.Range(1, keywordCount)
            .Select(sequence => new Guid(sequence + 1_000_000, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0))
            .ToList();

        foreach (var keywordId in keywordIds)
        {
            context.Keywords.Add(new Keyword
            {
                Id = keywordId,
                Name = $"bounded-term-{keywordId}",
                CanonicalName = $"bounded-term-{keywordId}",
                CreatedAt = now,
                UpdatedAt = now,
            });
        }

        await context.SaveChangesAsync();
        context.ChangeTracker.Clear();

        var repository = new KeywordLocalizationBackfillRepository(context);
        var processedKeywordIds = new HashSet<Guid>();
        var ourKeywordIds = keywordIds.ToHashSet();

        for (var run = 0; run < keywordCount / maxItems; run++)
        {
            var candidates = await repository.SelectCandidatesAsync(locale, maxItems, startAfterKeywordId: null);
            Assert.Equal(maxItems, candidates.Count);
            Assert.All(candidates, candidate => ourKeywordIds.Contains(candidate.KeywordId));
            Assert.All(candidates, candidate => Assert.DoesNotContain(candidate.KeywordId, processedKeywordIds));

            foreach (var candidate in candidates)
            {
                processedKeywordIds.Add(candidate.KeywordId);
            }

            var upserts = candidates
                .Select(candidate => new KeywordLocalizationMachineUpsert(
                    candidate.KeywordId,
                    locale,
                    $"TR:{candidate.SourceText}",
                    KeywordDiscoverLocalizationSupport.NormalizeSearchName($"TR:{candidate.SourceText}"),
                    candidate.SourceTextHash,
                    DateTime.UtcNow))
                .ToList();

            var upsertResult = await repository.UpsertMachineTranslationsAsync(upserts);
            Assert.Equal(maxItems, upsertResult.Inserted);
            Assert.Equal(0, upsertResult.Updated);
            Assert.Equal(0, upsertResult.SkippedProtected);
        }

        Assert.Equal(keywordCount, processedKeywordIds.Count);

        var noWorkLeft = await repository.SelectCandidatesAsync(locale, maxItems, startAfterKeywordId: null);
        Assert.Empty(noWorkLeft);

        await using var verifyContext = CatalogPersistenceFixture.CreateContext();
        var localizedCount = await verifyContext.KeywordLocalizations
            .Where(localization => localization.Locale == locale && ourKeywordIds.Contains(localization.KeywordId))
            .CountAsync();
        Assert.Equal(keywordCount, localizedCount);
    }

    [Fact]
    public async Task ConcurrentMachineUpsertsOnMissingLocalizationProduceSingleRow()
    {
        await using var context = CatalogPersistenceFixture.CreateContext();
        var keywordId = new Guid(99, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0);
        var now = DateTime.UtcNow;
        context.Keywords.Add(new Keyword
        {
            Id = keywordId,
            Name = "time travel",
            CanonicalName = "time travel",
            CreatedAt = now,
            UpdatedAt = now,
        });
        await context.SaveChangesAsync();

        var hash = KeywordLocalizationSourceText.ComputeSourceTextHash("time travel");
        using var barrier = new Barrier(2);

        var results = await Task.WhenAll(
            RunConcurrentMachineUpsertAsync(keywordId, hash, "Machine A", barrier),
            RunConcurrentMachineUpsertAsync(keywordId, hash, "Machine B", barrier));

        Assert.Equal(2, results.Sum(result => result.Inserted + result.Updated));
        Assert.Equal(0, results.Sum(result => result.SkippedProtected));

        await using var verifyContext = CatalogPersistenceFixture.CreateContext();
        var rows = await verifyContext.KeywordLocalizations
            .Where(localization => localization.KeywordId == keywordId)
            .ToListAsync();
        Assert.Single(rows);
        Assert.Equal(KeywordTranslationSource.Machine, rows[0].TranslationSource);
    }

    [Fact]
    public async Task ConcurrentMachineUpsertsAgainstCuratedLocalizationAreSkipped()
    {
        await using var context = CatalogPersistenceFixture.CreateContext();
        var keywordId = new Guid(100, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0);
        var now = DateTime.UtcNow;
        context.Keywords.Add(new Keyword
        {
            Id = keywordId,
            Name = "time travel",
            CanonicalName = "time travel",
            CreatedAt = now,
            UpdatedAt = now,
        });
        context.KeywordLocalizations.Add(new KeywordLocalization
        {
            KeywordId = keywordId,
            Locale = SupportedContentLocales.TurkishTurkey,
            Name = "Zaman yolculuğu",
            NormalizedName = KeywordDiscoverLocalizationSupport.NormalizeSearchName("Zaman yolculuğu"),
            TranslationSource = KeywordTranslationSource.Curated,
            ReviewStatus = KeywordTranslationReviewStatus.Reviewed,
            SourceTextHash = "curated-hash",
            CreatedAtUtc = now,
            UpdatedAtUtc = now,
        });
        await context.SaveChangesAsync();

        var hash = KeywordLocalizationSourceText.ComputeSourceTextHash("time travel");
        using var barrier = new Barrier(2);

        var results = await Task.WhenAll(
            RunConcurrentMachineUpsertAsync(keywordId, hash, "Machine A", barrier),
            RunConcurrentMachineUpsertAsync(keywordId, hash, "Machine B", barrier));

        Assert.Equal(0, results.Sum(result => result.Inserted + result.Updated));
        Assert.Equal(2, results.Sum(result => result.SkippedProtected));

        await using var verifyContext = CatalogPersistenceFixture.CreateContext();
        var row = await verifyContext.KeywordLocalizations.SingleAsync(
            localization => localization.KeywordId == keywordId);
        Assert.Equal("Zaman yolculuğu", row.Name);
        Assert.Equal(KeywordTranslationSource.Curated, row.TranslationSource);
    }

    private static Task<KeywordLocalizationMachineUpsertResult> RunConcurrentMachineUpsertAsync(
        Guid keywordId,
        string sourceTextHash,
        string translatedName,
        Barrier barrier) =>
        Task.Run(async () =>
        {
            await using var context = CatalogPersistenceFixture.CreateContext();
            var repository = new KeywordLocalizationBackfillRepository(context);
            barrier.SignalAndWait();
            return await repository.UpsertMachineTranslationsAsync(
                [
                    new KeywordLocalizationMachineUpsert(
                        keywordId,
                        SupportedContentLocales.TurkishTurkey,
                        translatedName,
                        KeywordDiscoverLocalizationSupport.NormalizeSearchName(translatedName),
                        sourceTextHash,
                        DateTime.UtcNow),
                ]);
        });

    [Fact]
    public async Task LegacyLocalizationAfterGovernanceMigrationIsNotMachineOverwritten()
    {
        await using var context = CatalogPersistenceFixture.CreateContext();
        var keywordId = Guid.NewGuid();
        var locale = SupportedContentLocales.TurkishTurkey;
        var now = DateTime.UtcNow;
        context.Keywords.Add(new Keyword
        {
            Id = keywordId,
            Name = "time travel",
            CanonicalName = "time travel",
            CreatedAt = now,
            UpdatedAt = now,
        });
        context.KeywordLocalizations.Add(new KeywordLocalization
        {
            KeywordId = keywordId,
            Locale = locale,
            Name = "Legacy çeviri",
            NormalizedName = KeywordDiscoverLocalizationSupport.NormalizeSearchName("Legacy çeviri"),
            TranslationSource = KeywordTranslationSource.Machine,
            ReviewStatus = KeywordTranslationReviewStatus.Unreviewed,
            SourceTextHash = string.Empty,
            CreatedAtUtc = now,
            UpdatedAtUtc = now,
        });
        await context.SaveChangesAsync();
        context.ChangeTracker.Clear();

        var repository = new KeywordLocalizationBackfillRepository(context);
        var atRiskCandidates = await repository.SelectCandidatesAsync(locale, 25, startAfterKeywordId: null);
        Assert.Contains(atRiskCandidates, candidate => candidate.KeywordId == keywordId);

        await context.Database.ExecuteSqlInterpolatedAsync(
            $"""
             UPDATE keyword_localizations
             SET "ReviewStatus" = {(int)KeywordTranslationReviewStatus.Reviewed}
             WHERE "ReviewStatus" = {(int)KeywordTranslationReviewStatus.Unreviewed}
               AND "KeywordId" = {keywordId}
               AND "Locale" = {locale}
             """);

        var protectedCandidates = await repository.SelectCandidatesAsync(locale, 25, startAfterKeywordId: null);
        Assert.DoesNotContain(protectedCandidates, candidate => candidate.KeywordId == keywordId);

        var newHash = KeywordLocalizationSourceText.ComputeSourceTextHash("time travel");
        var upsertResult = await repository.UpsertMachineTranslationsAsync(
            [
                new KeywordLocalizationMachineUpsert(
                    keywordId,
                    locale,
                    "Azure overwrite attempt",
                    "azure overwrite attempt",
                    newHash,
                    DateTime.UtcNow),
            ]);

        Assert.Equal(0, upsertResult.Inserted);
        Assert.Equal(0, upsertResult.Updated);
        Assert.Equal(1, upsertResult.SkippedProtected);

        await using var verifyContext = CatalogPersistenceFixture.CreateContext();
        var row = await verifyContext.KeywordLocalizations.SingleAsync(
            localization => localization.KeywordId == keywordId && localization.Locale == locale);
        Assert.Equal("Legacy çeviri", row.Name);
        Assert.Equal(KeywordTranslationReviewStatus.Reviewed, row.ReviewStatus);
        Assert.Equal(string.Empty, row.SourceTextHash);
        var expectedHash = KeywordLocalizationSourceText.ComputeSourceTextHash("time travel");
        Assert.NotEqual(expectedHash, row.SourceTextHash);
    }

    [Fact]
    public async Task UpsertMachineTranslationsDoesNotOverwriteCuratedLocalization()
    {
        await using var context = CatalogPersistenceFixture.CreateContext();
        var keywordId = Guid.NewGuid();
        var now = DateTime.UtcNow;
        context.Keywords.Add(new Keyword
        {
            Id = keywordId,
            Name = "time travel",
            CanonicalName = "time travel",
            CreatedAt = now,
            UpdatedAt = now,
        });
        context.KeywordLocalizations.Add(new KeywordLocalization
        {
            KeywordId = keywordId,
            Locale = SupportedContentLocales.TurkishTurkey,
            Name = "Zaman yolculuğu",
            NormalizedName = KeywordDiscoverLocalizationSupport.NormalizeSearchName("Zaman yolculuğu"),
            TranslationSource = KeywordTranslationSource.Curated,
            ReviewStatus = KeywordTranslationReviewStatus.Reviewed,
            SourceTextHash = "curated-hash",
            CreatedAtUtc = now,
            UpdatedAtUtc = now,
        });
        await context.SaveChangesAsync();

        var repository = new KeywordLocalizationBackfillRepository(context);
        var result = await repository.UpsertMachineTranslationsAsync(
            [
                new KeywordLocalizationMachineUpsert(
                    keywordId,
                    SupportedContentLocales.TurkishTurkey,
                    "Machine overwrite attempt",
                    "machine",
                    "new-hash",
                    DateTime.UtcNow),
            ]);

        Assert.Equal(0, result.Inserted);
        Assert.Equal(0, result.Updated);
        Assert.Equal(1, result.SkippedProtected);

        var row = await context.KeywordLocalizations.FindAsync(keywordId, SupportedContentLocales.TurkishTurkey);
        Assert.Equal("Zaman yolculuğu", row!.Name);
        Assert.Equal(KeywordTranslationSource.Curated, row.TranslationSource);
    }

    [Fact]
    public async Task UpsertMachineTranslationsUpdatesStaleMachineLocalization()
    {
        await using var context = CatalogPersistenceFixture.CreateContext();
        var keywordId = Guid.NewGuid();
        var now = DateTime.UtcNow;
        context.Keywords.Add(new Keyword
        {
            Id = keywordId,
            Name = "time travel",
            CanonicalName = "time travel",
            CreatedAt = now,
            UpdatedAt = now,
        });
        context.KeywordLocalizations.Add(new KeywordLocalization
        {
            KeywordId = keywordId,
            Locale = SupportedContentLocales.TurkishTurkey,
            Name = "Old machine",
            NormalizedName = "old machine",
            TranslationSource = KeywordTranslationSource.Machine,
            ReviewStatus = KeywordTranslationReviewStatus.Unreviewed,
            SourceTextHash = "stale",
            CreatedAtUtc = now,
            UpdatedAtUtc = now,
        });
        await context.SaveChangesAsync();

        var repository = new KeywordLocalizationBackfillRepository(context);
        var newHash = KeywordLocalizationSourceText.ComputeSourceTextHash("time travel");
        var result = await repository.UpsertMachineTranslationsAsync(
            [
                new KeywordLocalizationMachineUpsert(
                    keywordId,
                    SupportedContentLocales.TurkishTurkey,
                    "Zaman yolculuğu",
                    KeywordDiscoverLocalizationSupport.NormalizeSearchName("Zaman yolculuğu"),
                    newHash,
                    DateTime.UtcNow),
            ]);

        Assert.Equal(1, result.Updated);

        await using var verifyContext = CatalogPersistenceFixture.CreateContext();
        var row = await verifyContext.KeywordLocalizations.SingleAsync(
            localization => localization.KeywordId == keywordId);
        Assert.Equal("Zaman yolculuğu", row.Name);
        Assert.Equal(newHash, row.SourceTextHash);
    }

    private static async Task EnsureCurrentMachineLocalizationsForKeywordsAsync(
        ApplicationDbContext context,
        List<Guid> keywordIds,
        string locale,
        DateTime utcNow)
    {
        if (keywordIds.Count == 0)
        {
            return;
        }

        var keywords = await context.Keywords
            .AsNoTracking()
            .Where(keyword => keywordIds.Contains(keyword.Id))
            .Select(keyword => new { keyword.Id, keyword.Name, keyword.CanonicalName })
            .ToListAsync();

        var repository = new KeywordLocalizationBackfillRepository(context);
        var upserts = new List<KeywordLocalizationMachineUpsert>();
        foreach (var keyword in keywords)
        {
            var sourceText = KeywordLocalizationSourceText.ResolveSourceText(new Keyword
            {
                Name = keyword.Name,
                CanonicalName = keyword.CanonicalName,
            });
            var hash = KeywordLocalizationSourceText.ComputeSourceTextHash(
                KeywordLocalizationSourceText.NormalizeSourceTextForHash(sourceText));
            upserts.Add(new KeywordLocalizationMachineUpsert(
                keyword.Id,
                locale,
                sourceText,
                KeywordDiscoverLocalizationSupport.NormalizeSearchName(sourceText),
                hash,
                utcNow));
        }

        if (upserts.Count > 0)
        {
            await repository.UpsertMachineTranslationsAsync(upserts);
            context.ChangeTracker.Clear();
        }
    }
}
