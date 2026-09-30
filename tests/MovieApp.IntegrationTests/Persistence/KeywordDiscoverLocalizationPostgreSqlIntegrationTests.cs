using System.Globalization;
using MovieApp.Application.Services.Keywords;
using MovieApp.Application.Services.Localization;
using MovieApp.Domain.Entities;
using MovieApp.Domain.Enums;
using MovieApp.Infrastructure.Persistence;
using MovieApp.Infrastructure.Persistence.Repositories;

namespace MovieApp.IntegrationTests.Persistence;

[Collection("CatalogPersistence")]
public sealed class KeywordDiscoverLocalizationPostgreSqlIntegrationTests
{
    [Fact]
    public async Task SearchAsyncLocalizedAndCanonicalQueriesReturnOneResultWithTurkishDisplay()
    {
        await using var context = CatalogPersistenceFixture.CreateContext();
        var token = Guid.NewGuid().ToString("N");
        var canonicalName = UniqueName("pr1-time-travel");
        var turkishName = $"Zaman yolculuğu {token}";
        var keywordId = await SeedKeywordAsync(
            context,
            canonicalName: canonicalName,
            tmdbExternalId: UniqueTmdbId(),
            turkishName: turkishName);

        var repository = new KeywordDiscoverReadRepository(context);

        var localized = await repository.SearchAsync(
            turkishName,
            SupportedContentLocales.TurkishTurkey,
            1,
            10);
        var canonical = await repository.SearchAsync(
            canonicalName,
            SupportedContentLocales.TurkishTurkey,
            1,
            10);

        Assert.Equal(1, localized.TotalCount);
        Assert.Single(localized.Items);
        Assert.Equal(keywordId, localized.Items[0].KeywordId);
        Assert.Equal(turkishName, localized.Items[0].Name);

        Assert.Equal(1, canonical.TotalCount);
        Assert.Single(canonical.Items);
        Assert.Equal(keywordId, canonical.Items[0].KeywordId);
        Assert.Equal(turkishName, canonical.Items[0].Name);
    }

    [Fact]
    public async Task SearchAsyncDeduplicatesWhenLocalizedAndCanonicalBothMatch()
    {
        await using var context = CatalogPersistenceFixture.CreateContext();
        var canonical = UniqueName("pr1-dedupe-time-travel");
        var keywordId = await SeedKeywordAsync(
            context,
            canonicalName: canonical,
            tmdbExternalId: UniqueTmdbId(),
            turkishName: "Zaman yolculuğu");

        var repository = new KeywordDiscoverReadRepository(context);
        var result = await repository.SearchAsync(
            canonical,
            SupportedContentLocales.TurkishTurkey,
            1,
            20);

        Assert.Equal(1, result.TotalCount);
        Assert.Single(result.Items);
        Assert.Equal(keywordId, result.Items[0].KeywordId);
    }

    [Fact]
    public async Task SearchAsyncFallsBackToEnglishTranslationWhenRequestedLocaleMissing()
    {
        await using var context = CatalogPersistenceFixture.CreateContext();
        var canonical = UniqueName("pr1-en-fallback");
        await SeedKeywordAsync(
            context,
            canonicalName: canonical,
            tmdbExternalId: UniqueTmdbId(),
            englishName: "Time travel curated");

        var repository = new KeywordDiscoverReadRepository(context);
        var result = await repository.SearchAsync(
            "Time travel",
            SupportedContentLocales.TurkishTurkey,
            1,
            20);

        Assert.Single(result.Items);
        Assert.Equal("Time travel curated", result.Items[0].Name);
    }

    [Fact]
    public async Task SearchAsyncFallsBackToCanonicalNameWhenTranslationsMissing()
    {
        await using var context = CatalogPersistenceFixture.CreateContext();
        var canonical = UniqueName("pr1-canonical-only");
        var keywordId = await SeedKeywordAsync(
            context,
            canonicalName: canonical,
            tmdbExternalId: UniqueTmdbId());

        var repository = new KeywordDiscoverReadRepository(context);
        var result = await repository.SearchAsync(
            canonical,
            SupportedContentLocales.TurkishTurkey,
            1,
            20);

        Assert.Single(result.Items);
        Assert.Equal(keywordId, result.Items[0].KeywordId);
        Assert.Equal(canonical, result.Items[0].Name);
    }

    [Fact]
    public async Task SearchAsyncExcludesKeywordsWithoutTmdbReference()
    {
        await using var context = CatalogPersistenceFixture.CreateContext();
        var keywordId = Guid.NewGuid();
        var now = DateTime.UtcNow;
        var canonical = UniqueName("pr1-no-tmdb");
        context.Keywords.Add(new Keyword
        {
            Id = keywordId,
            Name = canonical,
            CanonicalName = canonical,
            NormalizedName = KeywordCanonicalNormalization.NormalizeKeywordName(canonical),
            SemanticCategory = KeywordSemanticCategory.Unknown,
            ClassificationStatus = KeywordClassificationStatus.Auto,
            CreatedAt = now,
            UpdatedAt = now,
        });
        context.KeywordLocalizations.Add(new KeywordLocalization
        {
            KeywordId = keywordId,
            Locale = SupportedContentLocales.TurkishTurkey,
            Name = "Yerelleştirilmiş",
            NormalizedName = KeywordDiscoverLocalizationSupport.NormalizeSearchName("Yerelleştirilmiş"),
            CreatedAtUtc = now,
            UpdatedAtUtc = now,
        });
        await context.SaveChangesAsync();

        var repository = new KeywordDiscoverReadRepository(context);
        var result = await repository.SearchAsync(
            "Yerelleştirilmiş",
            SupportedContentLocales.TurkishTurkey,
            1,
            20);

        Assert.Empty(result.Items);
    }

    [Fact]
    public async Task SearchAsyncDoesNotReturnSpanishAliasForTurkishLocale()
    {
        await using var context = CatalogPersistenceFixture.CreateContext();
        var canonical = UniqueName("pr1-es-isolation");
        await SeedKeywordAsync(
            context,
            canonicalName: canonical,
            tmdbExternalId: UniqueTmdbId(),
            spanishName: "Viajes en el tiempo");

        var repository = new KeywordDiscoverReadRepository(context);
        var result = await repository.SearchAsync(
            "Viajes",
            SupportedContentLocales.TurkishTurkey,
            1,
            20);

        Assert.Empty(result.Items);
    }

    [Fact]
    public async Task SearchAsyncRanksExactLocalizedMatchBeforeContains()
    {
        await using var context = CatalogPersistenceFixture.CreateContext();
        var exactId = await SeedKeywordAsync(
            context,
            canonicalName: UniqueName("pr1-rank-exact-base"),
            tmdbExternalId: UniqueTmdbId(),
            turkishName: "zaman");
        var containsId = await SeedKeywordAsync(
            context,
            canonicalName: UniqueName("pr1-rank-contains-base"),
            tmdbExternalId: UniqueTmdbId(),
            turkishName: "zaman yolculuğu");

        var repository = new KeywordDiscoverReadRepository(context);
        var result = await repository.SearchAsync(
            "zaman",
            SupportedContentLocales.TurkishTurkey,
            1,
            10);

        Assert.True(result.TotalCount >= 2);
        Assert.Equal(exactId, result.Items[0].KeywordId);
        Assert.Contains(result.Items, item => item.KeywordId == containsId);
        Assert.Equal(
            result.Items.Select(item => item.KeywordId).Distinct().Count(),
            result.Items.Count);
    }

    [Fact]
    public async Task SearchAsyncPaginationRanksExactMatchOnFirstPageAmongManyContainsMatches()
    {
        await using var context = CatalogPersistenceFixture.CreateContext();
        var token = Guid.NewGuid().ToString("N");
        var exactTurkish = $"xyzzy-{token}";
        var exactId = await SeedKeywordAsync(
            context,
            canonicalName: UniqueName($"pr1-rank-page-exact-{token}"),
            tmdbExternalId: UniqueTmdbId(),
            turkishName: exactTurkish);

        for (var index = 0; index < 12; index++)
        {
            await SeedKeywordAsync(
                context,
                canonicalName: UniqueName($"pr1-rank-page-contains-{token}-{index}"),
                tmdbExternalId: UniqueTmdbId(),
                turkishName: $"{exactTurkish}-padding-{index}");
        }

        var repository = new KeywordDiscoverReadRepository(context);
        var pageOne = await repository.SearchAsync(
            exactTurkish,
            SupportedContentLocales.TurkishTurkey,
            1,
            5);
        var pageTwo = await repository.SearchAsync(
            exactTurkish,
            SupportedContentLocales.TurkishTurkey,
            2,
            5);

        Assert.Equal(5, pageOne.Items.Count);
        Assert.Equal(exactId, pageOne.Items[0].KeywordId);
        Assert.Equal(exactTurkish, pageOne.Items[0].Name);
        Assert.True(pageTwo.Items.Count >= 1);
        Assert.Equal(
            pageOne.Items.Concat(pageTwo.Items).Select(item => item.KeywordId).Distinct().Count(),
            pageOne.Items.Count + pageTwo.Items.Count);
    }

    [Fact]
    public async Task SearchAsyncPaginationPreservesStableOrderingWithoutDuplicateIds()
    {
        await using var context = CatalogPersistenceFixture.CreateContext();
        for (var index = 0; index < 3; index++)
        {
            await SeedKeywordAsync(
                context,
                canonicalName: UniqueName($"pr1-page-{index}"),
                tmdbExternalId: UniqueTmdbId(),
                turkishName: $"pr1-pagination-{index}");
        }

        var repository = new KeywordDiscoverReadRepository(context);
        var pageOne = await repository.SearchAsync(
            "pr1-pagination",
            SupportedContentLocales.TurkishTurkey,
            1,
            2);
        var pageTwo = await repository.SearchAsync(
            "pr1-pagination",
            SupportedContentLocales.TurkishTurkey,
            2,
            2);

        Assert.Equal(2, pageOne.Items.Count);
        Assert.True(pageTwo.Items.Count >= 1);
        Assert.Equal(
            pageOne.Items.Count + pageTwo.Items.Count,
            pageOne.Items.Concat(pageTwo.Items).Select(item => item.KeywordId).Distinct().Count());
    }

    private static string UniqueName(string prefix) => $"{prefix}-{Guid.NewGuid():N}";

    private static string UniqueTmdbId() =>
        Random.Shared.Next(9_000_000, 9_999_999).ToString(CultureInfo.InvariantCulture);

    private static async Task<Guid> SeedKeywordAsync(
        ApplicationDbContext context,
        string canonicalName,
        string tmdbExternalId,
        string? turkishName = null,
        string? englishName = null,
        string? spanishName = null)
    {
        var keywordId = Guid.NewGuid();
        var now = DateTime.UtcNow;
        context.Keywords.Add(new Keyword
        {
            Id = keywordId,
            Name = canonicalName,
            CanonicalName = canonicalName,
            NormalizedName = KeywordCanonicalNormalization.NormalizeKeywordName(canonicalName),
            SemanticCategory = KeywordSemanticCategory.Unknown,
            ClassificationStatus = KeywordClassificationStatus.Auto,
            CreatedAt = now,
            UpdatedAt = now,
        });
        context.KeywordExternalReferences.Add(new KeywordExternalReference
        {
            KeywordId = keywordId,
            Provider = KeywordProvider.Tmdb,
            ExternalId = tmdbExternalId,
            ExternalName = canonicalName,
            CreatedAt = now,
        });

        if (turkishName is not null)
        {
            context.KeywordLocalizations.Add(new KeywordLocalization
            {
                KeywordId = keywordId,
                Locale = SupportedContentLocales.TurkishTurkey,
                Name = turkishName,
                NormalizedName = KeywordDiscoverLocalizationSupport.NormalizeSearchName(turkishName),
                CreatedAtUtc = now,
                UpdatedAtUtc = now,
            });
        }

        if (englishName is not null)
        {
            context.KeywordLocalizations.Add(new KeywordLocalization
            {
                KeywordId = keywordId,
                Locale = SupportedContentLocales.EnglishUnitedStates,
                Name = englishName,
                NormalizedName = KeywordDiscoverLocalizationSupport.NormalizeSearchName(englishName),
                CreatedAtUtc = now,
                UpdatedAtUtc = now,
            });
        }

        if (spanishName is not null)
        {
            context.KeywordLocalizations.Add(new KeywordLocalization
            {
                KeywordId = keywordId,
                Locale = SupportedContentLocales.SpanishSpain,
                Name = spanishName,
                NormalizedName = KeywordDiscoverLocalizationSupport.NormalizeSearchName(spanishName),
                CreatedAtUtc = now,
                UpdatedAtUtc = now,
            });
        }

        await context.SaveChangesAsync();
        return keywordId;
    }
}
