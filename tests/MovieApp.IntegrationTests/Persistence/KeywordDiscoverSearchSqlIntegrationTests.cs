using System.Globalization;
using Microsoft.EntityFrameworkCore;
using MovieApp.Application.Services.Keywords;
using MovieApp.Application.Services.Localization;
using MovieApp.Domain.Entities;
using MovieApp.Domain.Enums;
using MovieApp.Infrastructure.Persistence;
using MovieApp.Infrastructure.Persistence.Repositories;

namespace MovieApp.IntegrationTests.Persistence;

[Collection("CatalogPersistence")]
public sealed class KeywordDiscoverSearchSqlIntegrationTests
{
    [Fact]
    public async Task TotalCountMatchesLegacyEfMatchingQueryForTurkishLocale()
    {
        await using var context = CatalogPersistenceFixture.CreateContext();
        var token = Guid.NewGuid().ToString("N");
        await SeedDiscoverableKeywordAsync(context, $"kd2-count-a-{token}", turkishName: $"alpha-{token}");
        await SeedDiscoverableKeywordAsync(context, $"kd2-count-b-{token}", turkishName: $"beta-{token}");

        var locale = SupportedContentLocales.TurkishTurkey;
        var englishLocale = SupportedContentLocales.EnglishUnitedStates;
        var query = token;
        var pattern = $"%{query}%";
        var normalizedPattern = $"%{KeywordDiscoverLocalizationSupport.NormalizeSearchName(query)}%";

        var legacyCount = await KeywordDiscoverSearchQueryLegacyMatching
            .BuildMatchingKeywordIds(context, locale, englishLocale, isEnglishLocale: false, pattern, normalizedPattern)
            .CountAsync();

        var sqlCount = await KeywordDiscoverSearchQuery.GetTotalCountAsync(
            context,
            locale,
            englishLocale,
            isEnglishLocale: false,
            pattern,
            normalizedPattern,
            CancellationToken.None);

        Assert.Equal(legacyCount, sqlCount);
    }

    [Fact]
    public async Task TotalCountMatchesLegacyEfMatchingQueryForEnglishLocale()
    {
        await using var context = CatalogPersistenceFixture.CreateContext();
        var token = Guid.NewGuid().ToString("N");
        await SeedDiscoverableKeywordAsync(context, $"kd2-en-{token}", englishName: $"english-{token}");

        var locale = SupportedContentLocales.EnglishUnitedStates;
        var englishLocale = SupportedContentLocales.EnglishUnitedStates;
        var query = $"english-{token}";
        var pattern = $"%{query}%";
        var normalizedPattern = pattern;

        var legacyCount = await KeywordDiscoverSearchQueryLegacyMatching
            .BuildMatchingKeywordIds(context, locale, englishLocale, isEnglishLocale: true, pattern, normalizedPattern)
            .CountAsync();

        var sqlCount = await KeywordDiscoverSearchQuery.GetTotalCountAsync(
            context,
            locale,
            englishLocale,
            isEnglishLocale: true,
            pattern,
            normalizedPattern,
            CancellationToken.None);

        Assert.Equal(legacyCount, sqlCount);
    }

    [Fact]
    public async Task ConcatenatedPagesMatchSingleWidePageOrderingForDeterministicFixture()
    {
        await using var context = CatalogPersistenceFixture.CreateContext();
        var token = Guid.NewGuid().ToString("N");
        var exactId = await SeedDiscoverableKeywordAsync(
            context,
            $"kd2-page-exact-{token}",
            turkishName: $"zz-exact-{token}");
        for (var index = 0; index < 5; index++)
        {
            await SeedDiscoverableKeywordAsync(
                context,
                $"kd2-page-pad-{token}-{index}",
                turkishName: $"zz-exact-{token}-pad-{index}");
        }

        var repository = new KeywordDiscoverReadRepository(context);
        var query = $"zz-exact-{token}";
        const int pageSize = 2;

        var wide = await repository.SearchAsync(
            query,
            SupportedContentLocales.TurkishTurkey,
            1,
            20);
        var pageOne = await repository.SearchAsync(query, SupportedContentLocales.TurkishTurkey, 1, pageSize);
        var pageTwo = await repository.SearchAsync(query, SupportedContentLocales.TurkishTurkey, 2, pageSize);
        var pageThree = await repository.SearchAsync(query, SupportedContentLocales.TurkishTurkey, 3, pageSize);

        var pagedIds = pageOne.Items
            .Concat(pageTwo.Items)
            .Concat(pageThree.Items)
            .Select(item => item.KeywordId)
            .ToList();

        Assert.Equal(wide.TotalCount, pagedIds.Count);
        Assert.Equal(wide.Items.Select(item => item.KeywordId).ToList(), pagedIds);
        Assert.Equal(exactId, pageOne.Items[0].KeywordId);
        Assert.Equal(pagedIds.Count, pagedIds.Distinct().Count());
    }

    [Fact]
    public async Task ZeroResultQueryReturnsEmptyPageAndZeroTotalCount()
    {
        await using var context = CatalogPersistenceFixture.CreateContext();
        var repository = new KeywordDiscoverReadRepository(context);
        var result = await repository.SearchAsync(
            $"kd2-no-hit-{Guid.NewGuid():N}",
            SupportedContentLocales.TurkishTurkey,
            1,
            10);

        Assert.Equal(0, result.TotalCount);
        Assert.Empty(result.Items);
        Assert.Equal(0, result.TotalPages);
    }

    [Fact]
    public async Task PageSizeOneReturnsSingleItemPerPageWithoutDuplicates()
    {
        await using var context = CatalogPersistenceFixture.CreateContext();
        var token = Guid.NewGuid().ToString("N");
        for (var index = 0; index < 3; index++)
        {
            await SeedDiscoverableKeywordAsync(
                context,
                $"kd2-ps1-{token}-{index}",
                turkishName: $"kd2-ps1-{token}-{index}");
        }

        var repository = new KeywordDiscoverReadRepository(context);
        var query = $"kd2-ps1-{token}";
        var pageOne = await repository.SearchAsync(query, SupportedContentLocales.TurkishTurkey, 1, 1);
        var pageTwo = await repository.SearchAsync(query, SupportedContentLocales.TurkishTurkey, 2, 1);
        var pageThree = await repository.SearchAsync(query, SupportedContentLocales.TurkishTurkey, 3, 1);

        Assert.Single(pageOne.Items);
        Assert.Single(pageTwo.Items);
        Assert.Single(pageThree.Items);
        var ids = new[] { pageOne.Items[0].KeywordId, pageTwo.Items[0].KeywordId, pageThree.Items[0].KeywordId };
        Assert.Equal(3, ids.Distinct().Count());
    }

    private static async Task<Guid> SeedDiscoverableKeywordAsync(
        ApplicationDbContext context,
        string canonicalName,
        string? turkishName = null,
        string? englishName = null)
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
            ExternalId = Random.Shared.Next(9_000_000, 9_999_999).ToString(CultureInfo.InvariantCulture),
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

        await context.SaveChangesAsync();
        return keywordId;
    }
}
