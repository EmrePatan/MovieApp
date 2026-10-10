using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using MovieApp.Application.Configuration;
using MovieApp.Application.Models.Providers;
using MovieApp.Domain.Entities;
using MovieApp.Domain.Enums;
using MovieApp.Domain.Keywords;
using MovieApp.Infrastructure.Persistence;
using MovieApp.Infrastructure.Persistence.Keywords;
using MovieApp.Infrastructure.Persistence.Repositories;

namespace MovieApp.IntegrationTests.Persistence;

[Collection("CatalogPersistence")]
public sealed class KeywordGraphProviderAwareSyncIntegrationTests
{
    [Fact]
    public async Task ProviderAwareMovieSyncPersistsTmdbSourceAndJoinAgainstPostgreSql()
    {
        await using var context = CatalogPersistenceFixture.CreateContext();
        var movie = await SeedMovieAsync(context);
        var repository = new KeywordCatalogRepository(
            context,
            Options.Create(new KeywordGraphOptions { ProviderAwareSyncEnabled = true }));

        await repository.SyncMovieKeywordsAsync(
            movie.Id,
            [new ProviderKeywordSummary(4242, "sequel")],
            DateTime.UtcNow);

        var relationships = await context.MovieKeywords
            .AsNoTracking()
            .Where(join => join.MovieId == movie.Id)
            .ToListAsync();

        Assert.Single(relationships);
        Assert.True(KeywordProviderSources.Contains(relationships[0].Sources, KeywordProvider.Tmdb));
    }

    [Fact]
    public async Task ProviderAwareEmptyTmdbSyncKeepsMdbListSourceAndJoinAgainstPostgreSql()
    {
        await using var context = CatalogPersistenceFixture.CreateContext();
        var movie = await SeedMovieAsync(context);
        var keyword = await SeedKeywordAsync(context, Random.Shared.Next(3_000_000, 3_999_999), "revenge");
        context.MovieKeywords.Add(new MovieKeyword
        {
            MovieId = movie.Id,
            KeywordId = keyword.Id,
            Sources = KeywordProviderSources.SetProvider(
                KeywordProviderSources.Create(KeywordProvider.Tmdb),
                KeywordProvider.MdbList,
                include: true),
        });
        await context.SaveChangesAsync();

        var repository = new KeywordCatalogRepository(
            context,
            Options.Create(new KeywordGraphOptions { ProviderAwareSyncEnabled = true }));
        await repository.SyncMovieKeywordsAsync(movie.Id, [], DateTime.UtcNow);

        var relationship = await context.MovieKeywords
            .AsNoTracking()
            .SingleAsync(join => join.MovieId == movie.Id && join.KeywordId == keyword.Id);

        Assert.False(KeywordProviderSources.Contains(relationship.Sources, KeywordProvider.Tmdb));
        Assert.True(KeywordProviderSources.Contains(relationship.Sources, KeywordProvider.MdbList));
    }

    [Fact]
    public async Task LegacySyncMirrorsTmdbSourcesOnPostgreSql()
    {
        await using var context = CatalogPersistenceFixture.CreateContext();
        var movie = await SeedMovieAsync(context);
        var repository = new KeywordCatalogRepository(
            context,
            Options.Create(new KeywordGraphOptions { ProviderAwareSyncEnabled = false }));

        await repository.SyncMovieKeywordsAsync(
            movie.Id,
            [new ProviderKeywordSummary(Random.Shared.Next(4_000_000, 4_999_999), "shadow")],
            DateTime.UtcNow);

        var relationships = await context.MovieKeywords
            .AsNoTracking()
            .Where(join => join.MovieId == movie.Id)
            .ToListAsync();
        var joinKeywordIds = relationships
            .Select(join => join.KeywordId)
            .OrderBy(id => id)
            .ToList();
        var tmdbSourceKeywordIds = relationships
            .Where(join => KeywordProviderSources.Contains(join.Sources, KeywordProvider.Tmdb))
            .Select(join => join.KeywordId)
            .OrderBy(id => id)
            .ToList();

        Assert.Equal(joinKeywordIds, tmdbSourceKeywordIds);
    }

    [Fact]
    public async Task VerifyReadinessAsyncIsReadOnlyAgainstPostgreSql()
    {
        await using var context = CatalogPersistenceFixture.CreateContext();
        var service = new KeywordGraphReconciliationService(context);
        var beforeKeywords = await context.Keywords.CountAsync();
        var result = await service.VerifyReadinessAsync();
        Assert.Equal(beforeKeywords, await context.Keywords.CountAsync());
        Assert.NotNull(result);
    }

    [Fact]
    public async Task ReconcileDoesNotInferTmdbSourceFromJoinAgainstPostgreSql()
    {
        await using var setup = CatalogPersistenceFixture.CreateContext();
        var movie = await SeedMovieAsync(setup);
        var keyword = await SeedKeywordAsync(setup, 7777, "legacy");
        setup.MovieKeywords.Add(new MovieKeyword
        {
            MovieId = movie.Id,
            KeywordId = keyword.Id,
            Sources = KeywordProviderSources.Create(KeywordProvider.MdbList),
        });
        await setup.SaveChangesAsync();

        await using var reconcileContext = CatalogPersistenceFixture.CreateContext();
        var service = new KeywordGraphReconciliationService(reconcileContext);
        await service.ReconcileAsync();

        var relationship = await reconcileContext.MovieKeywords
            .AsNoTracking()
            .SingleAsync(join => join.MovieId == movie.Id && join.KeywordId == keyword.Id);

        Assert.False(KeywordProviderSources.Contains(relationship.Sources, KeywordProvider.Tmdb));
        Assert.True(KeywordProviderSources.Contains(relationship.Sources, KeywordProvider.MdbList));
    }

    private static async Task<Movie> SeedMovieAsync(ApplicationDbContext context)
    {
        var movie = new Movie
        {
            Id = Guid.NewGuid(),
            TmdbId = Random.Shared.Next(80_000_000, 89_999_999),
            Title = "Provider-aware probe",
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
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow,
        };
        context.Keywords.Add(keyword);
        await context.SaveChangesAsync();
        return keyword;
    }
}
