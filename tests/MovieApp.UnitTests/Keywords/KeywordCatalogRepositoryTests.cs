using Microsoft.EntityFrameworkCore;
using MovieApp.Application.Models.Providers;
using MovieApp.Domain.Entities;
using MovieApp.Infrastructure.Persistence;
using MovieApp.Infrastructure.Persistence.Repositories;

namespace MovieApp.UnitTests.Keywords;

public sealed class KeywordCatalogRepositoryTests
{
    [Fact]
    public async Task SyncMovieKeywordsAsyncPersistsKeywordsAndRelationships()
    {
        await using var context = CreateContext();
        var movie = await SeedMovieAsync(context, 27205);
        var repository = new KeywordCatalogRepository(context);
        var syncedAtUtc = new DateTime(2026, 9, 15, 12, 0, 0, DateTimeKind.Utc);

        await repository.SyncMovieKeywordsAsync(
            movie.Id,
            [new ProviderKeywordSummary(42, "time travel"), new ProviderKeywordSummary(99, "dream")],
            syncedAtUtc);

        Assert.Equal(2, await context.Keywords.CountAsync());
        Assert.Equal(2, await context.MovieKeywords.CountAsync());
        Assert.Equal(syncedAtUtc, (await context.Movies.SingleAsync()).KeywordsSyncedAtUtc);
    }

    [Fact]
    public async Task SyncMovieKeywordsAsyncDeduplicatesProviderKeywordIds()
    {
        await using var context = CreateContext();
        var movie = await SeedMovieAsync(context, 27205);
        var repository = new KeywordCatalogRepository(context);

        await repository.SyncMovieKeywordsAsync(
            movie.Id,
            [
                new ProviderKeywordSummary(42, "time travel"),
                new ProviderKeywordSummary(42, "time travel"),
            ],
            DateTime.UtcNow);

        Assert.Equal(1, await context.Keywords.CountAsync());
        Assert.Equal(1, await context.MovieKeywords.CountAsync());
    }

    [Fact]
    public async Task SyncMovieKeywordsAsyncReusesExistingKeywordByTmdbId()
    {
        await using var context = CreateContext();
        var movie = await SeedMovieAsync(context, 27205);
        var existingKeyword = new Keyword
        {
            Id = Guid.NewGuid(),
            TmdbKeywordId = 42,
            Name = "old",
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow
        };
        context.Keywords.Add(existingKeyword);
        await context.SaveChangesAsync();

        var repository = new KeywordCatalogRepository(context);
        await repository.SyncMovieKeywordsAsync(
            movie.Id,
            [new ProviderKeywordSummary(42, "time travel")],
            DateTime.UtcNow);

        Assert.Equal(1, await context.Keywords.CountAsync());
        Assert.Equal("time travel", (await context.Keywords.SingleAsync()).Name);
    }

    [Fact]
    public async Task SyncMovieKeywordsAsyncRemovesStaleRelationshipsAndAddsNewOnes()
    {
        await using var context = CreateContext();
        var movie = await SeedMovieAsync(context, 27205);
        var keywordA = await SeedKeywordAsync(context, 1, "alpha");
        var keywordB = await SeedKeywordAsync(context, 2, "beta");
        context.MovieKeywords.AddRange(
            new MovieKeyword { MovieId = movie.Id, KeywordId = keywordA.Id },
            new MovieKeyword { MovieId = movie.Id, KeywordId = keywordB.Id });
        await context.SaveChangesAsync();

        var repository = new KeywordCatalogRepository(context);
        await repository.SyncMovieKeywordsAsync(
            movie.Id,
            [new ProviderKeywordSummary(2, "beta"), new ProviderKeywordSummary(3, "gamma")],
            DateTime.UtcNow);

        var linkedTmdbIds = await context.MovieKeywords
            .Where(movieKeyword => movieKeyword.MovieId == movie.Id)
            .Select(movieKeyword => movieKeyword.Keyword.TmdbKeywordId)
            .OrderBy(tmdbId => tmdbId)
            .ToListAsync();

        Assert.Equal([2, 3], linkedTmdbIds);
        Assert.Equal(3, await context.Keywords.CountAsync());
    }

    [Fact]
    public async Task SyncMovieKeywordsAsyncClearsRelationshipsOnSuccessfulEmptyResponse()
    {
        await using var context = CreateContext();
        var movie = await SeedMovieAsync(context, 27205);
        var keyword = await SeedKeywordAsync(context, 1, "alpha");
        context.MovieKeywords.Add(new MovieKeyword { MovieId = movie.Id, KeywordId = keyword.Id });
        await context.SaveChangesAsync();

        var repository = new KeywordCatalogRepository(context);
        var syncedAtUtc = DateTime.UtcNow;
        await repository.SyncMovieKeywordsAsync(movie.Id, [], syncedAtUtc);

        Assert.Empty(await context.MovieKeywords.ToListAsync());
        Assert.Equal(1, await context.Keywords.CountAsync());
        Assert.Equal(syncedAtUtc, (await context.Movies.SingleAsync()).KeywordsSyncedAtUtc);
    }

    [Fact]
    public async Task SyncTvShowKeywordsAsyncMapsTvKeywords()
    {
        await using var context = CreateContext();
        var tvShow = await SeedTvShowAsync(context, 1396);
        var repository = new KeywordCatalogRepository(context);

        await repository.SyncTvShowKeywordsAsync(
            tvShow.Id,
            [new ProviderKeywordSummary(9715, "drug dealer")],
            DateTime.UtcNow);

        Assert.Equal(1, await context.TvShowKeywords.CountAsync());
        Assert.NotNull((await context.TvShows.SingleAsync()).KeywordsSyncedAtUtc);
    }

    [Fact]
    public async Task SharedKeywordIsReusedAcrossMovieAndTvShow()
    {
        await using var context = CreateContext();
        var movie = await SeedMovieAsync(context, 27205);
        var tvShow = await SeedTvShowAsync(context, 1396);
        var repository = new KeywordCatalogRepository(context);

        await repository.SyncMovieKeywordsAsync(
            movie.Id,
            [new ProviderKeywordSummary(42, "shared keyword")],
            DateTime.UtcNow);
        await repository.SyncTvShowKeywordsAsync(
            tvShow.Id,
            [new ProviderKeywordSummary(42, "shared keyword")],
            DateTime.UtcNow);

        Assert.Equal(1, await context.Keywords.CountAsync());
        Assert.Equal(1, await context.MovieKeywords.CountAsync());
        Assert.Equal(1, await context.TvShowKeywords.CountAsync());
    }

    [Fact]
    public async Task RepeatedSynchronizationIsIdempotent()
    {
        await using var context = CreateContext();
        var movie = await SeedMovieAsync(context, 27205);
        var repository = new KeywordCatalogRepository(context);
        var keywords = new[] { new ProviderKeywordSummary(42, "time travel") };

        await repository.SyncMovieKeywordsAsync(movie.Id, keywords, DateTime.UtcNow);
        await repository.SyncMovieKeywordsAsync(movie.Id, keywords, DateTime.UtcNow);

        Assert.Equal(1, await context.Keywords.CountAsync());
        Assert.Equal(1, await context.MovieKeywords.CountAsync());
    }

    private static ApplicationDbContext CreateContext()
    {
        var options = new DbContextOptionsBuilder<ApplicationDbContext>()
            .UseInMemoryDatabase($"keyword-repository-{Guid.NewGuid()}")
            .Options;

        return new ApplicationDbContext(options);
    }

    private static async Task<Movie> SeedMovieAsync(ApplicationDbContext context, int tmdbId)
    {
        var movie = new Movie
        {
            Id = Guid.NewGuid(),
            TmdbId = tmdbId,
            Title = "Test Movie",
            VoteAverage = 8,
            VoteCount = 10,
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
            Title = "Test Show",
            Status = MovieApp.Domain.Enums.TvShowStatus.Ended,
            VoteAverage = 8,
            VoteCount = 10,
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow
        };

        context.TvShows.Add(tvShow);
        await context.SaveChangesAsync();
        return tvShow;
    }

    private static async Task<Keyword> SeedKeywordAsync(
        ApplicationDbContext context,
        int tmdbKeywordId,
        string name)
    {
        var keyword = new Keyword
        {
            Id = Guid.NewGuid(),
            TmdbKeywordId = tmdbKeywordId,
            Name = name,
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow
        };

        context.Keywords.Add(keyword);
        await context.SaveChangesAsync();
        return keyword;
    }
}
