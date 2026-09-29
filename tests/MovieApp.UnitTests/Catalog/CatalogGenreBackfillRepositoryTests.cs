using Microsoft.EntityFrameworkCore;
using MovieApp.Domain.Entities;
using MovieApp.Domain.Enums;
using MovieApp.Infrastructure.Persistence;
using MovieApp.Infrastructure.Persistence.Repositories;

namespace MovieApp.UnitTests.Catalog;

public sealed class CatalogGenreBackfillRepositoryTests
{
    [Fact]
    public async Task SelectMovieCandidatesAsyncReturnsOnlyGenreLessMoviesWithTmdbId()
    {
        await using var context = CreateContext();
        var eligible = await SeedMovieAsync(context, 101, withGenres: false);
        await SeedMovieAsync(context, 102, withGenres: true);
        await SeedMovieAsync(context, null, withGenres: false);
        var repository = new CatalogGenreBackfillRepository(context);

        var candidates = await repository.SelectMovieCandidatesAsync(10, []);

        Assert.Single(candidates);
        Assert.Equal(eligible, candidates[0].CatalogId);
    }

    [Fact]
    public async Task SelectTvShowCandidatesAsyncReturnsOnlyGenreLessTvShowsWithTmdbId()
    {
        await using var context = CreateContext();
        var eligible = await SeedTvShowAsync(context, 201, withGenres: false);
        await SeedTvShowAsync(context, 202, withGenres: true);
        await SeedTvShowAsync(context, null, withGenres: false);
        var repository = new CatalogGenreBackfillRepository(context);

        var candidates = await repository.SelectTvShowCandidatesAsync(10, []);

        Assert.Single(candidates);
        Assert.Equal(eligible, candidates[0].CatalogId);
    }

    [Fact]
    public async Task SelectMovieCandidatesAsyncRespectsExcludeIds()
    {
        await using var context = CreateContext();
        var first = await SeedMovieAsync(context, 301, withGenres: false);
        await SeedMovieAsync(context, 302, withGenres: false);
        var repository = new CatalogGenreBackfillRepository(context);

        var candidates = await repository.SelectMovieCandidatesAsync(10, [first]);

        Assert.Single(candidates);
        Assert.NotEqual(first, candidates[0].CatalogId);
    }

    [Fact]
    public async Task GetCoverageAsyncCountsGenreLessTitles()
    {
        await using var context = CreateContext();
        await SeedMovieAsync(context, 401, withGenres: true);
        await SeedMovieAsync(context, 402, withGenres: false);
        await SeedTvShowAsync(context, 501, withGenres: false);
        var repository = new CatalogGenreBackfillRepository(context);

        var coverage = await repository.GetCoverageAsync();

        Assert.Equal(1, coverage.MovieGenreLess);
        Assert.Equal(2, coverage.MovieEligible);
        Assert.Equal(1, coverage.TvGenreLess);
        Assert.Equal(2, coverage.OverallRemaining);
    }

    private static ApplicationDbContext CreateContext()
    {
        var options = new DbContextOptionsBuilder<ApplicationDbContext>()
            .UseInMemoryDatabase($"catalog-genre-backfill-{Guid.NewGuid()}")
            .Options;

        return new ApplicationDbContext(options);
    }

    private static async Task<Guid> SeedMovieAsync(
        ApplicationDbContext context,
        int? tmdbId,
        bool withGenres)
    {
        var movieId = Guid.NewGuid();
        context.Movies.Add(new Movie
        {
            Id = movieId,
            TmdbId = tmdbId,
            Title = $"Movie {tmdbId}",
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow
        });

        if (withGenres)
        {
            var genre = new Genre
            {
                Id = Guid.NewGuid(),
                Name = $"Genre-{movieId:N}",
                CreatedAt = DateTime.UtcNow
            };
            context.Genres.Add(genre);
            context.MovieGenres.Add(new MovieGenre
            {
                MovieId = movieId,
                GenreId = genre.Id
            });
        }

        await context.SaveChangesAsync();
        return movieId;
    }

    private static async Task<Guid> SeedTvShowAsync(
        ApplicationDbContext context,
        int? tmdbId,
        bool withGenres)
    {
        var tvShowId = Guid.NewGuid();
        context.TvShows.Add(new TvShow
        {
            Id = tvShowId,
            TmdbId = tmdbId,
            Title = $"Tv {tmdbId}",
            Status = TvShowStatus.Ended,
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow
        });

        if (withGenres)
        {
            var genre = new Genre
            {
                Id = Guid.NewGuid(),
                Name = $"TvGenre-{tvShowId:N}",
                CreatedAt = DateTime.UtcNow
            };
            context.Genres.Add(genre);
            context.TvShowGenres.Add(new TvShowGenre
            {
                TvShowId = tvShowId,
                GenreId = genre.Id
            });
        }

        await context.SaveChangesAsync();
        return tvShowId;
    }
}
