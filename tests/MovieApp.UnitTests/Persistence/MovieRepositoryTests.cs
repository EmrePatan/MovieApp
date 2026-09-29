using Microsoft.EntityFrameworkCore;
using MovieApp.Application.Mapping;
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
        var repository = CatalogRepositoryTestFactory.CreateMovieRepository(context);

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
        var repository = CatalogRepositoryTestFactory.CreateMovieRepository(context);

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
        var repository = CatalogRepositoryTestFactory.CreateMovieRepository(context);

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
        var repository = CatalogRepositoryTestFactory.CreateMovieRepository(context);

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

    [Fact]
    public async Task EnsureFromSummariesAsyncReusesExistingMovieAppGuid()
    {
        var options = new DbContextOptionsBuilder<ApplicationDbContext>()
            .UseInMemoryDatabase($"movie-repository-summary-{Guid.NewGuid()}")
            .Options;

        await using var context = new ApplicationDbContext(options);
        var repository = CatalogRepositoryTestFactory.CreateMovieRepository(context);

        var details = CreateDetails(
            tmdbId: 12345,
            imdbId: "tt12345",
            title: "Existing Movie");

        var existing = await repository.UpsertFromProviderAsync(details);
        var resolved = await repository.EnsureFromSummariesAsync(
        [
            new MovieProviderSummary(
                "fake-tmdb-12345",
                12345,
                null,
                "tt12345",
                "Existing Movie",
                "Overview",
                new DateOnly(2010, 1, 1),
                "/poster.jpg",
                8m,
                100)
        ]);

        Assert.Equal(existing.Id, resolved[12345]);
        Assert.Equal(1, await context.Movies.CountAsync());
    }

    [Fact]
    public async Task EnsureFromSummariesAsyncPersistsGenresForNewSummaryRow()
    {
        await using var context = CreateContext();
        var repository = CatalogRepositoryTestFactory.CreateMovieRepository(context);

        await repository.EnsureFromSummariesAsync([CreateSummary(54321, [28, 18])]);

        Assert.Equal(1, await context.Movies.CountAsync());
        Assert.Equal(2, await context.MovieGenres.CountAsync());
        var genreNames = await context.MovieGenres
            .Join(context.Genres, link => link.GenreId, genre => genre.Id, (_, genre) => genre.Name)
            .OrderBy(name => name)
            .ToListAsync();
        Assert.Equal(["Action", "Drama"], genreNames);
    }

    [Fact]
    public async Task EnsureFromSummariesAsyncIgnoresUnknownGenreIds()
    {
        await using var context = CreateContext();
        var repository = CatalogRepositoryTestFactory.CreateMovieRepository(context);

        await repository.EnsureFromSummariesAsync([CreateSummary(54322, [28, 999_999])]);

        Assert.Equal(1, await context.MovieGenres.CountAsync());
    }

    [Fact]
    public async Task EnsureFromSummariesAsyncRepairsExistingGenreLessRow()
    {
        await using var context = CreateContext();
        var repository = CatalogRepositoryTestFactory.CreateMovieRepository(context);

        await repository.EnsureFromSummariesAsync([CreateSummary(54323, genreIds: null)]);
        Assert.Equal(0, await context.MovieGenres.CountAsync());

        await repository.EnsureFromSummariesAsync([CreateSummary(54323, [80])]);

        Assert.Equal(1, await context.MovieGenres.CountAsync());
        var genreName = await context.MovieGenres
            .Join(context.Genres, link => link.GenreId, genre => genre.Id, (_, genre) => genre.Name)
            .SingleAsync();
        Assert.Equal("Crime", genreName);
    }

    [Fact]
    public async Task EnsureFromSummariesAsyncDoesNotReplaceExistingGenres()
    {
        await using var context = CreateContext();
        var repository = CatalogRepositoryTestFactory.CreateMovieRepository(context);

        var existing = await repository.UpsertFromProviderAsync(
            CreateDetails(tmdbId: 54324, imdbId: null, title: "Existing") with
            {
                Genres = ["Drama", "Thriller"]
            });

        await repository.EnsureFromSummariesAsync([CreateSummary(54324, [28])]);

        var links = await context.MovieGenres.Where(link => link.MovieId == existing.Id).CountAsync();
        Assert.Equal(2, links);
    }

    [Fact]
    public async Task EnsureFromSummariesAsyncDoesNotDuplicateGenreLinksOnRepeatIngestion()
    {
        await using var context = CreateContext();
        var repository = CatalogRepositoryTestFactory.CreateMovieRepository(context);
        var summary = CreateSummary(54325, [28, 18]);

        await repository.EnsureFromSummariesAsync([summary]);
        await repository.EnsureFromSummariesAsync([summary]);

        Assert.Equal(2, await context.MovieGenres.CountAsync());
    }

    [Fact]
    public async Task EnsureFromSummariesAsyncAllowsEmptyGenreIds()
    {
        await using var context = CreateContext();
        var repository = CatalogRepositoryTestFactory.CreateMovieRepository(context);

        await repository.EnsureFromSummariesAsync([CreateSummary(54326, [])]);

        Assert.Equal(1, await context.Movies.CountAsync());
        Assert.Equal(0, await context.MovieGenres.CountAsync());
    }

    [Fact]
    public void MapMovieTmdbIdsToGenreNamesUsesMovieGenreMapping()
    {
        var names = TmdbGenreIdMap.MapMovieTmdbIdsToGenreNames([28, 878]);

        Assert.Equal(["Action", "Science Fiction"], names);
    }

    [Fact]
    public async Task UpsertFromProviderAsyncPersistsCollectionFields()
    {
        var options = new DbContextOptionsBuilder<ApplicationDbContext>()
            .UseInMemoryDatabase($"movie-repository-collection-{Guid.NewGuid()}")
            .Options;

        await using var context = new ApplicationDbContext(options);
        var repository = CatalogRepositoryTestFactory.CreateMovieRepository(context);

        var details = CreateDetails(
            tmdbId: 900001,
            imdbId: "tt9000001",
            title: "Interstellar") with
        {
            TmdbCollectionId = 645,
            CollectionName = "Space Collection",
            CollectionPosterPath = "/collection-poster.jpg",
            CollectionBackdropPath = "/collection-backdrop.jpg"
        };

        var movie = await repository.UpsertFromProviderAsync(details);

        Assert.Equal(645, movie.TmdbCollectionId);
        Assert.Equal("Space Collection", movie.CollectionName);
        Assert.Equal("/collection-poster.jpg", movie.CollectionPosterPath);
        Assert.Equal("/collection-backdrop.jpg", movie.CollectionBackdropPath);
    }

    [Fact]
    public async Task UpsertFromProviderAsyncClearsStaleCollectionFieldsOnReIngest()
    {
        var options = new DbContextOptionsBuilder<ApplicationDbContext>()
            .UseInMemoryDatabase($"movie-repository-clear-collection-{Guid.NewGuid()}")
            .Options;

        await using var context = new ApplicationDbContext(options);
        var repository = CatalogRepositoryTestFactory.CreateMovieRepository(context);

        var withCollection = CreateDetails(
            tmdbId: 900001,
            imdbId: "tt9000001",
            title: "Interstellar") with
        {
            TmdbCollectionId = 645,
            CollectionName = "Space Collection",
            CollectionPosterPath = "/collection-poster.jpg",
            CollectionBackdropPath = "/collection-backdrop.jpg"
        };

        await repository.UpsertFromProviderAsync(withCollection);

        var withoutCollection = CreateDetails(
            tmdbId: 900001,
            imdbId: "tt9000001",
            title: "Interstellar");

        var updated = await repository.UpsertFromProviderAsync(withoutCollection);

        Assert.Null(updated.TmdbCollectionId);
        Assert.Null(updated.CollectionName);
        Assert.Null(updated.CollectionPosterPath);
        Assert.Null(updated.CollectionBackdropPath);
    }

    private static ApplicationDbContext CreateContext()
    {
        var options = new DbContextOptionsBuilder<ApplicationDbContext>()
            .UseInMemoryDatabase($"movie-repository-summary-{Guid.NewGuid()}")
            .Options;

        return new ApplicationDbContext(options);
    }

    private static MovieProviderSummary CreateSummary(int tmdbId, IReadOnlyList<int>? genreIds) =>
        new(
            $"fake-tmdb-{tmdbId}",
            tmdbId,
            null,
            null,
            "New Movie",
            "Overview",
            new DateOnly(2020, 1, 1),
            "/poster.jpg",
            7.5m,
            50,
            GenreTmdbIds: genreIds);

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

