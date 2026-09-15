using Microsoft.EntityFrameworkCore;
using MovieApp.Domain.Entities;
using MovieApp.Domain.Enums;
using MovieApp.Infrastructure.Persistence;
using MovieApp.Infrastructure.Persistence.Repositories;

namespace MovieApp.UnitTests.Keywords;

public sealed class CatalogKeywordBackfillRepositoryTests
{
    [Fact]
    public async Task SelectMovieCandidatesAsyncReturnsOnlyUnsyncedMoviesWithTmdbId()
    {
        await using var context = CreateContext();
        var eligible = await SeedMovieAsync(context, 101, synced: false);
        await SeedMovieAsync(context, 102, synced: true);
        await SeedMovieAsync(context, null, synced: false);
        var repository = new CatalogKeywordBackfillRepository(context);

        var candidates = await repository.SelectMovieCandidatesAsync(10, []);

        Assert.Single(candidates);
        Assert.Equal(eligible, candidates[0].CatalogId);
    }

    [Fact]
    public async Task SelectTvShowCandidatesAsyncReturnsOnlyUnsyncedTvShowsWithTmdbId()
    {
        await using var context = CreateContext();
        var eligible = await SeedTvShowAsync(context, 201, synced: false);
        await SeedTvShowAsync(context, 202, synced: true);
        await SeedTvShowAsync(context, null, synced: false);
        var repository = new CatalogKeywordBackfillRepository(context);

        var candidates = await repository.SelectTvShowCandidatesAsync(10, []);

        Assert.Single(candidates);
        Assert.Equal(eligible, candidates[0].CatalogId);
    }

    [Fact]
    public async Task SelectMovieCandidatesAsyncPrioritizesInteractedTitles()
    {
        await using var context = CreateContext();
        var userId = Guid.NewGuid();
        var interactedMovieId = await SeedMovieAsync(context, 301, synced: false, voteCount: 10);
        var popularMovieId = await SeedMovieAsync(context, 302, synced: false, voteCount: 1000);
        context.Favorites.Add(new Favorite
        {
            Id = Guid.NewGuid(),
            UserId = userId,
            MovieId = interactedMovieId,
            CreatedAt = DateTime.UtcNow
        });
        await context.SaveChangesAsync();

        var repository = new CatalogKeywordBackfillRepository(context);
        var candidates = await repository.SelectMovieCandidatesAsync(1, []);

        Assert.Equal(interactedMovieId, candidates[0].CatalogId);
        Assert.NotEqual(popularMovieId, candidates[0].CatalogId);
    }

    [Fact]
    public async Task GetCoverageAsyncExcludesTitlesWithoutTmdbIdFromDenominator()
    {
        await using var context = CreateContext();
        await SeedMovieAsync(context, 401, synced: true);
        await SeedMovieAsync(context, 402, synced: false);
        await SeedMovieAsync(context, null, synced: false);
        await SeedTvShowAsync(context, 501, synced: true);
        await SeedTvShowAsync(context, null, synced: false);
        var repository = new CatalogKeywordBackfillRepository(context);

        var coverage = await repository.GetCoverageAsync();

        Assert.Equal(2, coverage.MovieEligible);
        Assert.Equal(1, coverage.MovieSynced);
        Assert.Equal(1, coverage.MovieUnsynced);
        Assert.Equal(50m, coverage.MovieCoveragePercent);
        Assert.Equal(1, coverage.TvEligible);
        Assert.Equal(100m, coverage.TvCoveragePercent);
        Assert.Equal(3, coverage.OverallEligible);
        Assert.Equal(66.67m, coverage.OverallCoveragePercent);
    }

    [Fact]
    public async Task GetCoverageAsyncReturnsZeroPercentWhenNoEligibleTitles()
    {
        await using var context = CreateContext();
        var repository = new CatalogKeywordBackfillRepository(context);

        var coverage = await repository.GetCoverageAsync();

        Assert.Equal(0m, coverage.MovieCoveragePercent);
        Assert.Equal(0m, coverage.TvCoveragePercent);
        Assert.Equal(0m, coverage.OverallCoveragePercent);
    }

    private static ApplicationDbContext CreateContext()
    {
        var options = new DbContextOptionsBuilder<ApplicationDbContext>()
            .UseInMemoryDatabase($"catalog-keyword-backfill-{Guid.NewGuid()}")
            .Options;

        return new ApplicationDbContext(options);
    }

    private static async Task<Guid> SeedMovieAsync(
        ApplicationDbContext context,
        int? tmdbId,
        bool synced,
        int voteCount = 100)
    {
        var movie = new Movie
        {
            Id = Guid.NewGuid(),
            TmdbId = tmdbId,
            Title = $"Movie {tmdbId}",
            VoteAverage = 8,
            VoteCount = voteCount,
            KeywordsSyncedAtUtc = synced ? DateTime.UtcNow : null,
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow
        };
        context.Movies.Add(movie);
        await context.SaveChangesAsync();
        return movie.Id;
    }

    private static async Task<Guid> SeedTvShowAsync(
        ApplicationDbContext context,
        int? tmdbId,
        bool synced,
        int voteCount = 100)
    {
        var tvShow = new TvShow
        {
            Id = Guid.NewGuid(),
            TmdbId = tmdbId,
            Title = $"Show {tmdbId}",
            Status = TvShowStatus.Ended,
            VoteAverage = 8,
            VoteCount = voteCount,
            KeywordsSyncedAtUtc = synced ? DateTime.UtcNow : null,
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow
        };
        context.TvShows.Add(tvShow);
        await context.SaveChangesAsync();
        return tvShow.Id;
    }
}
