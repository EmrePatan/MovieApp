using System.Globalization;
using Microsoft.EntityFrameworkCore;
using MovieApp.Application.Models.Providers;
using MovieApp.Application.Services.Keywords;
using MovieApp.Domain.Entities;
using MovieApp.Domain.Enums;
using MovieApp.Infrastructure.Persistence;
using MovieApp.Infrastructure.Persistence.Keywords;

namespace MovieApp.UnitTests.Keywords;

public sealed class KeywordGraphProviderAwareSyncTests
{
    [Fact]
    public async Task FlagFalseLegacySyncMirrorsTmdbSourcesWithoutChangingJoinSemantics()
    {
        await using var context = CreateContext();
        var movie = await SeedMovieAsync(context);
        var repository = KeywordCatalogRepositoryTestHelper.CreateRepository(context);

        await repository.SyncMovieKeywordsAsync(
            movie.Id,
            [new ProviderKeywordSummary(42, "revenge")],
            DateTime.UtcNow);

        Assert.Single(await context.MovieKeywords.ToListAsync());
        Assert.Single(await context.MovieKeywordSources.Where(source => source.Provider == KeywordProvider.Tmdb).ToListAsync());
    }

    [Fact]
    public async Task FlagFalseLegacyEmptyTmdbClearsJoinsButLeavesMdbListSources()
    {
        await using var context = CreateContext();
        var movie = await SeedMovieAsync(context);
        var keyword = await SeedKeywordAsync(context, 7, "revenge");
        context.MovieKeywordSources.Add(new MovieKeywordSource
        {
            MovieId = movie.Id,
            KeywordId = keyword.Id,
            Provider = KeywordProvider.MdbList,
            FirstSeenAtUtc = DateTime.UtcNow,
            LastSeenAtUtc = DateTime.UtcNow,
        });
        context.MovieKeywords.Add(new MovieKeyword { MovieId = movie.Id, KeywordId = keyword.Id });
        await context.SaveChangesAsync();

        var repository = KeywordCatalogRepositoryTestHelper.CreateRepository(context);
        await repository.SyncMovieKeywordsAsync(movie.Id, [], DateTime.UtcNow);

        Assert.Empty(await context.MovieKeywords.ToListAsync());
        Assert.Single(await context.MovieKeywordSources.Where(source => source.Provider == KeywordProvider.MdbList).ToListAsync());
        Assert.Empty(await context.MovieKeywordSources.Where(source => source.Provider == KeywordProvider.Tmdb).ToListAsync());
    }

    [Fact]
    public async Task FlagFalseSyncCreatesDualWriteMetadataAndShadowTmdbSources()
    {
        await using var context = CreateContext();
        var movie = await SeedMovieAsync(context);
        var repository = KeywordCatalogRepositoryTestHelper.CreateRepository(context);

        await repository.SyncMovieKeywordsAsync(
            movie.Id,
            [new ProviderKeywordSummary(42, "Time Travel")],
            DateTime.UtcNow);

        var keyword = await context.Keywords.SingleAsync();
        Assert.Equal("Time Travel", keyword.CanonicalName);
        Assert.Equal(
            KeywordCanonicalNormalization.NormalizeKeywordName("Time Travel"),
            keyword.NormalizedName);
        Assert.Single(await context.KeywordExternalReferences.ToListAsync());
        Assert.Single(await context.MovieKeywordSources.Where(source => source.Provider == KeywordProvider.Tmdb).ToListAsync());
        Assert.Single(await context.MovieKeywords.ToListAsync());
    }

    [Fact]
    public async Task ProviderAwareSyncCreatesTmdbSourceAndMaterializedJoin()
    {
        await using var context = CreateContext();
        var movie = await SeedMovieAsync(context);
        var repository = KeywordCatalogRepositoryTestHelper.CreateRepository(context, providerAwareSyncEnabled: true);

        await repository.SyncMovieKeywordsAsync(
            movie.Id,
            [new ProviderKeywordSummary(42, "revenge")],
            DateTime.UtcNow);

        Assert.Single(await context.MovieKeywordSources.Where(source => source.Provider == KeywordProvider.Tmdb).ToListAsync());
        Assert.Single(await context.MovieKeywords.ToListAsync());
    }

    [Fact]
    public async Task ProviderAwareEmptyTmdbSyncKeepsMdbListSourceAndJoin()
    {
        await using var context = CreateContext();
        var movie = await SeedMovieAsync(context);
        var keyword = await SeedKeywordAsync(context, 7, "revenge");
        context.MovieKeywordSources.Add(new MovieKeywordSource
        {
            MovieId = movie.Id,
            KeywordId = keyword.Id,
            Provider = KeywordProvider.MdbList,
            FirstSeenAtUtc = DateTime.UtcNow,
            LastSeenAtUtc = DateTime.UtcNow,
        });
        context.MovieKeywordSources.Add(new MovieKeywordSource
        {
            MovieId = movie.Id,
            KeywordId = keyword.Id,
            Provider = KeywordProvider.Tmdb,
            FirstSeenAtUtc = DateTime.UtcNow,
            LastSeenAtUtc = DateTime.UtcNow,
        });
        context.MovieKeywords.Add(new MovieKeyword { MovieId = movie.Id, KeywordId = keyword.Id });
        await context.SaveChangesAsync();

        var repository = KeywordCatalogRepositoryTestHelper.CreateRepository(context, providerAwareSyncEnabled: true);
        var syncedAtUtc = DateTime.UtcNow;
        await repository.SyncMovieKeywordsAsync(movie.Id, [], syncedAtUtc);

        Assert.DoesNotContain(
            await context.MovieKeywordSources.ToListAsync(),
            source => source.Provider == KeywordProvider.Tmdb);
        Assert.Contains(
            await context.MovieKeywordSources.ToListAsync(),
            source => source.Provider == KeywordProvider.MdbList);
        Assert.Single(await context.MovieKeywords.ToListAsync());
        Assert.Equal(syncedAtUtc, (await context.Movies.SingleAsync()).KeywordsSyncedAtUtc);
    }

    [Fact]
    public async Task ProviderAwareEmptyTmdbSyncRemovesJoinWhenOnlyTmdbSourceExisted()
    {
        await using var context = CreateContext();
        var movie = await SeedMovieAsync(context);
        var keyword = await SeedKeywordAsync(context, 7, "revenge");
        context.MovieKeywordSources.Add(new MovieKeywordSource
        {
            MovieId = movie.Id,
            KeywordId = keyword.Id,
            Provider = KeywordProvider.Tmdb,
            FirstSeenAtUtc = DateTime.UtcNow,
            LastSeenAtUtc = DateTime.UtcNow,
        });
        context.MovieKeywords.Add(new MovieKeyword { MovieId = movie.Id, KeywordId = keyword.Id });
        await context.SaveChangesAsync();

        var repository = KeywordCatalogRepositoryTestHelper.CreateRepository(context, providerAwareSyncEnabled: true);
        await repository.SyncMovieKeywordsAsync(movie.Id, [], DateTime.UtcNow);

        Assert.Empty(await context.MovieKeywordSources.ToListAsync());
        Assert.Empty(await context.MovieKeywords.ToListAsync());
    }

    [Fact]
    public async Task ProviderAwareSyncDoesNotRemoveMdbListSourceOnRefresh()
    {
        await using var context = CreateContext();
        var movie = await SeedMovieAsync(context);
        var keywordA = await SeedKeywordAsync(context, 1, "alpha");
        var keywordB = await SeedKeywordAsync(context, 2, "beta");
        context.MovieKeywordSources.Add(new MovieKeywordSource
        {
            MovieId = movie.Id,
            KeywordId = keywordB.Id,
            Provider = KeywordProvider.MdbList,
            FirstSeenAtUtc = DateTime.UtcNow,
            LastSeenAtUtc = DateTime.UtcNow,
        });
        context.MovieKeywords.Add(new MovieKeyword { MovieId = movie.Id, KeywordId = keywordB.Id });
        await context.SaveChangesAsync();

        var repository = KeywordCatalogRepositoryTestHelper.CreateRepository(context, providerAwareSyncEnabled: true);
        await repository.SyncMovieKeywordsAsync(
            movie.Id,
            [new ProviderKeywordSummary(1, "alpha")],
            DateTime.UtcNow);

        Assert.Contains(
            await context.MovieKeywordSources.ToListAsync(),
            source => source.Provider == KeywordProvider.MdbList && source.KeywordId == keywordB.Id);
        Assert.Equal(2, await context.MovieKeywords.CountAsync());
    }

    [Fact]
    public async Task ProviderAwareDualProviderProducesOneJoinAndTwoSources()
    {
        await using var context = CreateContext();
        var movie = await SeedMovieAsync(context);
        var keyword = await SeedKeywordAsync(context, 42, "shared");
        context.MovieKeywordSources.Add(new MovieKeywordSource
        {
            MovieId = movie.Id,
            KeywordId = keyword.Id,
            Provider = KeywordProvider.MdbList,
            FirstSeenAtUtc = DateTime.UtcNow,
            LastSeenAtUtc = DateTime.UtcNow,
        });
        await context.SaveChangesAsync();

        var repository = KeywordCatalogRepositoryTestHelper.CreateRepository(context, providerAwareSyncEnabled: true);
        await repository.SyncMovieKeywordsAsync(
            movie.Id,
            [new ProviderKeywordSummary(42, "shared")],
            DateTime.UtcNow);

        Assert.Equal(2, await context.MovieKeywordSources.CountAsync());
        Assert.Single(await context.MovieKeywords.ToListAsync());
    }

    [Fact]
    public async Task ReconciliationUsesInvariantCultureNormalizationForTurkishCapitalI()
    {
        await using var context = CreateContext();
        var keyword = new Keyword
        {
            Id = Guid.NewGuid(),
            TmdbKeywordId = 9001,
            Name = "İstanbul",
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow,
        };
        context.Keywords.Add(keyword);
        await context.SaveChangesAsync();

        var service = new KeywordGraphReconciliationService(context);
        var result = await service.ReconcileAsync();

        var stored = await context.Keywords.SingleAsync();
        Assert.Equal("İstanbul", stored.CanonicalName);
        Assert.Equal("İstanbul", stored.NormalizedName);
        Assert.True(result.IsReadyForProviderAwareSync);
    }

    [Fact]
    public async Task DualWriteThrowsOnConflictingTmdbExternalReferenceOwner()
    {
        await using var context = CreateContext();
        var keywordA = await SeedKeywordAsync(context, 100, "owner");
        var keywordB = new Keyword
        {
            Id = Guid.NewGuid(),
            TmdbKeywordId = 200,
            Name = "intruder",
            CanonicalName = "intruder",
            NormalizedName = "intruder",
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow,
        };
        context.Keywords.Add(keywordB);
        context.KeywordExternalReferences.Add(new KeywordExternalReference
        {
            KeywordId = keywordA.Id,
            Provider = KeywordProvider.Tmdb,
            ExternalId = "200",
            ExternalName = "wrong owner",
            CreatedAt = DateTime.UtcNow,
        });
        await context.SaveChangesAsync();

        var movie = await SeedMovieAsync(context);
        var repository = KeywordCatalogRepositoryTestHelper.CreateRepository(context);

        await Assert.ThrowsAsync<MovieApp.Application.Models.Keywords.KeywordGraphDataIntegrityException>(
            () => repository.SyncMovieKeywordsAsync(
                movie.Id,
                [new ProviderKeywordSummary(200, "intruder")],
                DateTime.UtcNow));
    }

    [Fact]
    public async Task ReconciliationDetectsConflictingExternalReference()
    {
        await using var context = CreateContext();
        var keyword = new Keyword
        {
            Id = Guid.NewGuid(),
            TmdbKeywordId = 100,
            Name = "test",
            CanonicalName = "test",
            NormalizedName = "test",
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow,
        };
        context.Keywords.Add(keyword);
        context.KeywordExternalReferences.Add(new KeywordExternalReference
        {
            KeywordId = keyword.Id,
            Provider = KeywordProvider.Tmdb,
            ExternalId = "999",
            ExternalName = "test",
            CreatedAt = DateTime.UtcNow,
        });
        await context.SaveChangesAsync();

        var service = new KeywordGraphReconciliationService(context);
        var result = await service.ReconcileAsync();

        Assert.False(result.IsReadyForProviderAwareSync);
        Assert.NotEmpty(result.Conflicts);
    }

    [Fact]
    public async Task ReconciliationBackfillsTmdbSourcesFromCurrentJoins()
    {
        await using var context = CreateContext();
        var movie = await SeedMovieAsync(context);
        var keyword = await SeedKeywordAsync(context, 55, "join only");
        context.MovieKeywords.Add(new MovieKeyword { MovieId = movie.Id, KeywordId = keyword.Id });
        await context.SaveChangesAsync();

        var service = new KeywordGraphReconciliationService(context);
        var first = await service.ReconcileAsync();
        var second = await service.ReconcileAsync();

        Assert.Equal(1, await context.MovieKeywordSources.CountAsync());
        Assert.Equal(0, first.MissingMovieTmdbSourceCount);
        Assert.Equal(0, second.MovieSourcesCreated);
        Assert.True(second.IsReadyForProviderAwareSync);
    }

    private static ApplicationDbContext CreateContext()
    {
        var options = new DbContextOptionsBuilder<ApplicationDbContext>()
            .UseInMemoryDatabase($"keyword-graph-pr2-{Guid.NewGuid()}")
            .Options;

        return new ApplicationDbContext(options);
    }

    private static async Task<Movie> SeedMovieAsync(ApplicationDbContext context)
    {
        var movie = new Movie
        {
            Id = Guid.NewGuid(),
            TmdbId = Random.Shared.Next(1_000_000, 9_999_999),
            Title = "Test",
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow,
        };
        context.Movies.Add(movie);
        await context.SaveChangesAsync();
        return movie;
    }

    private static async Task<Keyword> SeedKeywordAsync(ApplicationDbContext context, int tmdbId, string name)
    {
        var keyword = new Keyword
        {
            Id = Guid.NewGuid(),
            TmdbKeywordId = tmdbId,
            Name = name,
            CanonicalName = name,
            NormalizedName = KeywordCanonicalNormalization.NormalizeKeywordName(name),
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow,
        };
        context.Keywords.Add(keyword);
        await context.SaveChangesAsync();
        return keyword;
    }
}
