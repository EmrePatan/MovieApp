using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using MovieApp.Application.Configuration;
using MovieApp.Application.Models.Providers;
using MovieApp.Domain.Entities;
using MovieApp.Domain.Enums;
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

        Assert.Equal(1, await context.MovieKeywordSources.CountAsync(source => source.Provider == KeywordProvider.Tmdb));
        Assert.Equal(1, await context.MovieKeywords.CountAsync(join => join.MovieId == movie.Id));
    }

    [Fact]
    public async Task ProviderAwareEmptyTmdbSyncKeepsMdbListSourceAndJoinAgainstPostgreSql()
    {
        await using var context = CatalogPersistenceFixture.CreateContext();
        var movie = await SeedMovieAsync(context);
        var keyword = await SeedKeywordAsync(context, Random.Shared.Next(3_000_000, 3_999_999), "revenge");
        context.MovieKeywordSources.AddRange(
            new MovieKeywordSource
            {
                MovieId = movie.Id,
                KeywordId = keyword.Id,
                Provider = KeywordProvider.Tmdb,
                FirstSeenAtUtc = DateTime.UtcNow,
                LastSeenAtUtc = DateTime.UtcNow,
            },
            new MovieKeywordSource
            {
                MovieId = movie.Id,
                KeywordId = keyword.Id,
                Provider = KeywordProvider.MdbList,
                FirstSeenAtUtc = DateTime.UtcNow,
                LastSeenAtUtc = DateTime.UtcNow,
            });
        context.MovieKeywords.Add(new MovieKeyword { MovieId = movie.Id, KeywordId = keyword.Id });
        await context.SaveChangesAsync();

        var repository = new KeywordCatalogRepository(
            context,
            Options.Create(new KeywordGraphOptions { ProviderAwareSyncEnabled = true }));
        await repository.SyncMovieKeywordsAsync(movie.Id, [], DateTime.UtcNow);

        Assert.False(await context.MovieKeywordSources.AnyAsync(source =>
            source.MovieId == movie.Id && source.Provider == KeywordProvider.Tmdb));
        Assert.True(await context.MovieKeywordSources.AnyAsync(source =>
            source.MovieId == movie.Id && source.Provider == KeywordProvider.MdbList));
        Assert.Single(await context.MovieKeywords.Where(join => join.MovieId == movie.Id).ToListAsync());
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

        var joinKeywordIds = await context.MovieKeywords
            .Where(join => join.MovieId == movie.Id)
            .Select(join => join.KeywordId)
            .ToListAsync();
        var tmdbSourceKeywordIds = await context.MovieKeywordSources
            .Where(source => source.MovieId == movie.Id && source.Provider == KeywordProvider.Tmdb)
            .Select(source => source.KeywordId)
            .ToListAsync();

        Assert.Equal(joinKeywordIds.OrderBy(id => id), tmdbSourceKeywordIds.OrderBy(id => id));
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
        setup.MovieKeywordSources.Add(new MovieKeywordSource
        {
            MovieId = movie.Id,
            KeywordId = keyword.Id,
            Provider = KeywordProvider.MdbList,
            FirstSeenAtUtc = DateTime.UtcNow,
            LastSeenAtUtc = DateTime.UtcNow,
        });
        setup.MovieKeywords.Add(new MovieKeyword { MovieId = movie.Id, KeywordId = keyword.Id });
        await setup.SaveChangesAsync();

        await using var reconcileContext = CatalogPersistenceFixture.CreateContext();
        var service = new KeywordGraphReconciliationService(reconcileContext);
        await service.ReconcileAsync();

        Assert.False(await reconcileContext.MovieKeywordSources.AnyAsync(
            source => source.MovieId == movie.Id && source.Provider == KeywordProvider.Tmdb));
        Assert.True(await reconcileContext.MovieKeywordSources.AnyAsync(
            source => source.MovieId == movie.Id && source.Provider == KeywordProvider.MdbList));
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
