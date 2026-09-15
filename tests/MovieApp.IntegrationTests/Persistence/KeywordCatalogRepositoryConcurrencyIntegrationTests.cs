using Microsoft.EntityFrameworkCore;
using MovieApp.Application.Models.Providers;
using MovieApp.Domain.Entities;
using MovieApp.Domain.Enums;
using MovieApp.Infrastructure.Persistence;
using MovieApp.Infrastructure.Persistence.Repositories;

namespace MovieApp.IntegrationTests.Persistence;

[Collection("CatalogPersistence")]
public sealed class KeywordCatalogRepositoryConcurrencyIntegrationTests
{
    [Fact]
    public async Task ConcurrentMovieKeywordSyncConvergesOnSharedTmdbKeywordIdAgainstPostgreSql()
    {
        await using var setupContext = CatalogPersistenceFixture.CreateContext();
        var movieA = await SeedMovieAsync(setupContext, 438631);
        var movieB = await SeedMovieAsync(setupContext, 438632);
        var sharedKeyword = new ProviderKeywordSummary(9715, "drug dealer");
        var syncedAtUtc = DateTime.UtcNow;
        using var barrier = new Barrier(2);

        await Task.WhenAll(
            RunConcurrentMovieSyncAsync(movieA.Id, sharedKeyword, syncedAtUtc, barrier),
            RunConcurrentMovieSyncAsync(movieB.Id, sharedKeyword, syncedAtUtc, barrier));

        await using var verifyContext = CatalogPersistenceFixture.CreateContext();
        var canonicalKeywords = await verifyContext.Keywords
            .Where(keyword => keyword.TmdbKeywordId == sharedKeyword.TmdbKeywordId)
            .ToListAsync();

        Assert.Single(canonicalKeywords);
        var canonicalKeywordId = canonicalKeywords[0].Id;

        var linkedMovieIds = await verifyContext.MovieKeywords
            .Where(movieKeyword => movieKeyword.KeywordId == canonicalKeywordId)
            .Select(movieKeyword => movieKeyword.MovieId)
            .ToListAsync();

        Assert.Equal(2, linkedMovieIds.Count);
        Assert.Contains(movieA.Id, linkedMovieIds);
        Assert.Contains(movieB.Id, linkedMovieIds);
        Assert.NotNull((await verifyContext.Movies.FindAsync(movieA.Id))!.KeywordsSyncedAtUtc);
        Assert.NotNull((await verifyContext.Movies.FindAsync(movieB.Id))!.KeywordsSyncedAtUtc);
    }

    [Fact]
    public async Task ConcurrentMovieAndTvKeywordSyncConvergesOnSharedTmdbKeywordIdAgainstPostgreSql()
    {
        await using var setupContext = CatalogPersistenceFixture.CreateContext();
        var movie = await SeedMovieAsync(setupContext, 550001);
        var tvShow = await SeedTvShowAsync(setupContext, 550002);
        var sharedKeyword = new ProviderKeywordSummary(180547, "sequel");
        var syncedAtUtc = DateTime.UtcNow;
        using var barrier = new Barrier(2);

        await Task.WhenAll(
            RunConcurrentMovieSyncAsync(movie.Id, sharedKeyword, syncedAtUtc, barrier),
            RunConcurrentTvShowSyncAsync(tvShow.Id, sharedKeyword, syncedAtUtc, barrier));

        await using var verifyContext = CatalogPersistenceFixture.CreateContext();
        var canonicalKeywords = await verifyContext.Keywords
            .Where(keyword => keyword.TmdbKeywordId == sharedKeyword.TmdbKeywordId)
            .ToListAsync();

        Assert.Single(canonicalKeywords);
        var canonicalKeywordId = canonicalKeywords[0].Id;

        Assert.True(await verifyContext.MovieKeywords.AnyAsync(
            movieKeyword => movieKeyword.MovieId == movie.Id &&
                            movieKeyword.KeywordId == canonicalKeywordId));
        Assert.True(await verifyContext.TvShowKeywords.AnyAsync(
            tvShowKeyword => tvShowKeyword.TvShowId == tvShow.Id &&
                             tvShowKeyword.KeywordId == canonicalKeywordId));
        Assert.NotNull((await verifyContext.Movies.FindAsync(movie.Id))!.KeywordsSyncedAtUtc);
        Assert.NotNull((await verifyContext.TvShows.FindAsync(tvShow.Id))!.KeywordsSyncedAtUtc);
    }

    private static Task RunConcurrentMovieSyncAsync(
        Guid movieId,
        ProviderKeywordSummary keyword,
        DateTime syncedAtUtc,
        Barrier barrier) =>
        Task.Run(async () =>
        {
            await using var context = CatalogPersistenceFixture.CreateContext();
            var repository = new KeywordCatalogRepository(context);
            barrier.SignalAndWait();
            await repository.SyncMovieKeywordsAsync(movieId, [keyword], syncedAtUtc);
        });

    private static Task RunConcurrentTvShowSyncAsync(
        Guid tvShowId,
        ProviderKeywordSummary keyword,
        DateTime syncedAtUtc,
        Barrier barrier) =>
        Task.Run(async () =>
        {
            await using var context = CatalogPersistenceFixture.CreateContext();
            var repository = new KeywordCatalogRepository(context);
            barrier.SignalAndWait();
            await repository.SyncTvShowKeywordsAsync(tvShowId, [keyword], syncedAtUtc);
        });

    private static async Task<Movie> SeedMovieAsync(ApplicationDbContext context, int tmdbId)
    {
        var movie = new Movie
        {
            Id = Guid.NewGuid(),
            TmdbId = tmdbId,
            Title = $"Keyword Concurrency Movie {tmdbId}",
            VoteAverage = 7,
            VoteCount = 100,
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow
        };

        context.Movies.Add(movie);
        await context.SaveChangesAsync();
        return movie;
    }

    private static async Task<TvShow> SeedTvShowAsync(ApplicationDbContext context, int tmdbId)
    {
        var tvShow = new TvShow
        {
            Id = Guid.NewGuid(),
            TmdbId = tmdbId,
            Title = $"Keyword Concurrency Show {tmdbId}",
            Status = TvShowStatus.Ended,
            VoteAverage = 7,
            VoteCount = 100,
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow
        };

        context.TvShows.Add(tvShow);
        await context.SaveChangesAsync();
        return tvShow;
    }
}
