using MovieApp.Domain.Entities;
using MovieApp.Domain.Enums;
using MovieApp.Infrastructure.Persistence;
using MovieApp.Infrastructure.Persistence.Repositories;
using Microsoft.EntityFrameworkCore;

namespace MovieApp.UnitTests.Keywords;

public sealed class KeywordDiscoverReadRepositoryTests
{
    [Fact]
    public async Task ResolveTmdbKeywordIdsAsyncIgnoresExcludedKeywords()
    {
        await using var context = CreateContext();
        var excludedKeyword = new Keyword
        {
            Id = Guid.NewGuid(),
            Name = "spam metadata",
            ClassificationStatus = KeywordClassificationStatus.Excluded,
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow,
        };
        context.Keywords.Add(excludedKeyword);
        context.KeywordExternalReferences.Add(new KeywordExternalReference
        {
            KeywordId = excludedKeyword.Id,
            Provider = KeywordProvider.Tmdb,
            ExternalId = "9999",
            ExternalName = "spam metadata",
            CreatedAt = DateTime.UtcNow,
        });
        await context.SaveChangesAsync();

        var repository = new KeywordDiscoverReadRepository(context);
        var resolved = await repository.ResolveTmdbKeywordIdsAsync([excludedKeyword.Id]);

        Assert.Empty(resolved);
    }

    [Fact]
    public async Task ResolveTmdbKeywordIdsAsyncUsesTmdbExternalReferences()
    {
        await using var context = CreateContext();
        var keywordWithTmdb = new Keyword
        {
            Id = Guid.NewGuid(),
            Name = "time travel",
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow,
        };
        var keywordMdbOnly = new Keyword
        {
            Id = Guid.NewGuid(),
            Name = "mdb only",
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow,
        };
        context.Keywords.AddRange(keywordWithTmdb, keywordMdbOnly);
        context.KeywordExternalReferences.AddRange(
            new KeywordExternalReference
            {
                KeywordId = keywordWithTmdb.Id,
                Provider = KeywordProvider.Tmdb,
                ExternalId = "9715",
                ExternalName = "time travel",
                CreatedAt = DateTime.UtcNow,
            },
            new KeywordExternalReference
            {
                KeywordId = keywordMdbOnly.Id,
                Provider = KeywordProvider.MdbList,
                ExternalId = "9715",
                ExternalName = "mdb-time-travel",
                CreatedAt = DateTime.UtcNow,
            });
        await context.SaveChangesAsync();

        var repository = new KeywordDiscoverReadRepository(context);
        var resolved = await repository.ResolveTmdbKeywordIdsAsync(
            [keywordWithTmdb.Id, keywordMdbOnly.Id, keywordWithTmdb.Id]);

        Assert.Equal([9715], resolved);
    }

    [Fact]
    public async Task ResolveTmdbKeywordIdsAsyncFallsBackToKeywordsTmdbKeywordIdColumn()
    {
        await using var context = CreateContext();
        var keyword = new Keyword
        {
            Id = Guid.NewGuid(),
            Name = "zombie",
            TmdbKeywordId = 12345,
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow,
        };
        context.Keywords.Add(keyword);
        await context.SaveChangesAsync();

        var repository = new KeywordDiscoverReadRepository(context);
        var resolved = await repository.ResolveTmdbKeywordIdsAsync([keyword.Id]);

        Assert.Equal([12345], resolved);
    }

    [Fact]
    public async Task ResolveTmdbKeywordIdsAsyncPrefersExternalReferenceOverColumn()
    {
        await using var context = CreateContext();
        var keyword = new Keyword
        {
            Id = Guid.NewGuid(),
            Name = "time travel",
            TmdbKeywordId = 99999,
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow,
        };
        context.Keywords.Add(keyword);
        context.KeywordExternalReferences.Add(new KeywordExternalReference
        {
            KeywordId = keyword.Id,
            Provider = KeywordProvider.Tmdb,
            ExternalId = "9715",
            ExternalName = "time travel",
            CreatedAt = DateTime.UtcNow,
        });
        await context.SaveChangesAsync();

        var repository = new KeywordDiscoverReadRepository(context);
        var resolved = await repository.ResolveTmdbKeywordIdsAsync([keyword.Id]);

        Assert.Equal([9715], resolved);
    }

    [Fact]
    public async Task ResolveTmdbKeywordIdsAsyncBatchesMultipleKeywordsInTwoQueries()
    {
        await using var context = CreateContext();
        var first = Guid.NewGuid();
        var second = Guid.NewGuid();
        context.Keywords.AddRange(
            new Keyword
            {
                Id = first,
                Name = "a",
                TmdbKeywordId = 11,
                CreatedAt = DateTime.UtcNow,
                UpdatedAt = DateTime.UtcNow,
            },
            new Keyword
            {
                Id = second,
                Name = "b",
                CreatedAt = DateTime.UtcNow,
                UpdatedAt = DateTime.UtcNow,
            });
        context.KeywordExternalReferences.Add(new KeywordExternalReference
        {
            KeywordId = second,
            Provider = KeywordProvider.Tmdb,
            ExternalId = "22",
            ExternalName = "b",
            CreatedAt = DateTime.UtcNow,
        });
        await context.SaveChangesAsync();

        var repository = new KeywordDiscoverReadRepository(context);
        var resolved = await repository.ResolveTmdbKeywordIdsAsync([first, second]);

        Assert.Equal(2, resolved.Count);
        Assert.Contains(11, resolved);
        Assert.Contains(22, resolved);
    }

    private static ApplicationDbContext CreateContext()
    {
        var options = new DbContextOptionsBuilder<ApplicationDbContext>()
            .UseInMemoryDatabase($"keyword-discover-{Guid.NewGuid()}")
            .Options;
        return new ApplicationDbContext(options);
    }
}
