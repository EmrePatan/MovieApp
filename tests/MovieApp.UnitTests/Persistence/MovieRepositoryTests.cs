using Microsoft.EntityFrameworkCore;
using MovieApp.Application.Models.Providers;
using MovieApp.Infrastructure.Persistence;
using MovieApp.Infrastructure.Persistence.Repositories;

namespace MovieApp.UnitTests.Persistence;

public sealed class MovieRepositoryTests
{
    [Fact]
    public async Task UpsertFromProviderAsyncPreservesInternalIdForDuplicateExternalId()
    {
        var options = new DbContextOptionsBuilder<ApplicationDbContext>()
            .UseInMemoryDatabase($"movie-repository-{Guid.NewGuid()}")
            .Options;

        await using var context = new ApplicationDbContext(options);
        var repository = new MovieRepository(context);

        var details = new MovieProviderDetails(
            ExternalId: "fake-tmdb-900001",
            TmdbId: 900001,
            TvdbId: 900002,
            ImdbId: "tt9000001",
            Title: "Interstellar",
            OriginalTitle: "Interstellar",
            Overview: "Overview",
            ReleaseDate: new DateOnly(2014, 11, 7),
            RuntimeMinutes: 169,
            PosterPath: "/poster.jpg",
            BackdropPath: "/backdrop.jpg",
            OriginalLanguage: "en",
            VoteAverage: 8.7m,
            VoteCount: 100,
            Genres: ["Adventure", "Drama"]);

        var created = await repository.UpsertFromProviderAsync(details);
        var updated = await repository.UpsertFromProviderAsync(
            details with { VoteCount = 200, Overview = "Updated overview" });

        Assert.Equal(created.Id, updated.Id);
        Assert.Equal(200, updated.VoteCount);
        Assert.Equal(1, await context.Movies.CountAsync());
        Assert.Equal(2, await context.Genres.CountAsync());
        Assert.Equal(2, await context.MovieGenres.CountAsync());
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    public async Task UpsertFromProviderAsyncPersistsNullForEmptyOrWhitespaceImdbId(string imdbId)
    {
        var options = new DbContextOptionsBuilder<ApplicationDbContext>()
            .UseInMemoryDatabase($"movie-repository-empty-imdb-{Guid.NewGuid()}")
            .Options;

        await using var context = new ApplicationDbContext(options);
        var repository = new MovieRepository(context);

        var details = CreateDetails(
            tmdbId: 348369,
            imdbId: imdbId,
            title: "Avatar Days");

        var movie = await repository.UpsertFromProviderAsync(details);

        Assert.Null(movie.ImdbId);
        Assert.Null((await context.Movies.SingleAsync()).ImdbId);
    }

    [Fact]
    public async Task UpsertFromProviderAsyncPreservesValidImdbId()
    {
        var options = new DbContextOptionsBuilder<ApplicationDbContext>()
            .UseInMemoryDatabase($"movie-repository-valid-imdb-{Guid.NewGuid()}")
            .Options;

        await using var context = new ApplicationDbContext(options);
        var repository = new MovieRepository(context);

        var details = CreateDetails(
            tmdbId: 27205,
            imdbId: "tt1375666",
            title: "Inception");

        var movie = await repository.UpsertFromProviderAsync(details);

        Assert.Equal("tt1375666", movie.ImdbId);
    }

    [Fact]
    public async Task UpsertFromProviderAsyncStillLooksUpExistingMovieByTmdbId()
    {
        var options = new DbContextOptionsBuilder<ApplicationDbContext>()
            .UseInMemoryDatabase($"movie-repository-tmdb-lookup-{Guid.NewGuid()}")
            .Options;

        await using var context = new ApplicationDbContext(options);
        var repository = new MovieRepository(context);

        var details = CreateDetails(
            tmdbId: 900001,
            imdbId: "tt9000001",
            title: "Interstellar");

        var created = await repository.UpsertFromProviderAsync(details);
        var updated = await repository.UpsertFromProviderAsync(
            details with { Title = "Updated Interstellar", ImdbId = "   " });

        Assert.Equal(created.Id, updated.Id);
        Assert.Equal("Updated Interstellar", updated.Title);
        Assert.Null(updated.ImdbId);
        Assert.Equal(1, await context.Movies.CountAsync());
    }

    private static MovieProviderDetails CreateDetails(int tmdbId, string? imdbId, string title) =>
        new(
            ExternalId: $"fake-tmdb-{tmdbId}",
            TmdbId: tmdbId,
            TvdbId: null,
            ImdbId: imdbId,
            Title: title,
            OriginalTitle: title,
            Overview: "Overview",
            ReleaseDate: new DateOnly(2010, 7, 16),
            RuntimeMinutes: 120,
            PosterPath: "/poster.jpg",
            BackdropPath: "/backdrop.jpg",
            OriginalLanguage: "en",
            VoteAverage: 8.0m,
            VoteCount: 100,
            Genres: ["Drama"]);
}
