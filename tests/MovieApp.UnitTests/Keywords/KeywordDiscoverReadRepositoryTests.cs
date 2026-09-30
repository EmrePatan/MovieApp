using MovieApp.Domain.Entities;
using MovieApp.Domain.Enums;
using MovieApp.Infrastructure.Persistence;
using MovieApp.Infrastructure.Persistence.Repositories;
using Microsoft.EntityFrameworkCore;

namespace MovieApp.UnitTests.Keywords;

public sealed class KeywordDiscoverReadRepositoryTests
{
    [Fact]
    public async Task ResolveTmdbKeywordIdsAsyncUsesOnlyTmdbExternalReferences()
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

    private static ApplicationDbContext CreateContext()
    {
        var options = new DbContextOptionsBuilder<ApplicationDbContext>()
            .UseInMemoryDatabase($"keyword-discover-{Guid.NewGuid()}")
            .Options;
        return new ApplicationDbContext(options);
    }
}
