using Microsoft.EntityFrameworkCore;
using MovieApp.Domain.Entities;
using MovieApp.Domain.Enums;

namespace MovieApp.IntegrationTests.Persistence;

[CollectionDefinition("CatalogPersistence")]
public sealed class CatalogPersistenceTestsFixture : ICollectionFixture<CatalogPersistenceFixture>;

[Collection("CatalogPersistence")]
public sealed class CatalogPersistenceTests
{
    [Fact]
    public async Task DatabaseCanBeMigrated()
    {
        await using var context = CatalogPersistenceFixture.CreateContext();

        var pendingMigrations = await context.Database.GetPendingMigrationsAsync();

        Assert.Empty(pendingMigrations);
    }

    [Fact]
    public async Task MovieCanBePersistedWithUniqueExternalId()
    {
        await using var context = CatalogPersistenceFixture.CreateContext();

        var movie = CreateMovie(tmdbId: 1001);

        context.Movies.Add(movie);
        await context.SaveChangesAsync();

        var persisted = await context.Movies.SingleAsync(m => m.Id == movie.Id);

        Assert.Equal("Inception", persisted.Title);
        Assert.Equal(1001, persisted.TmdbId);
    }

    [Fact]
    public async Task TvShowSeasonAndEpisodeRelationshipsPersist()
    {
        await using var context = CatalogPersistenceFixture.CreateContext();

        var tvShow = CreateTvShow(tmdbId: 2001);
        var season = CreateSeason(tvShow, seasonNumber: 1, tmdbId: 3001);
        var episode = CreateEpisode(season, episodeNumber: 1, tmdbId: 4001);

        context.TvShows.Add(tvShow);
        context.Seasons.Add(season);
        context.Episodes.Add(episode);
        await context.SaveChangesAsync();

        var persistedEpisode = await context.Episodes
            .Include(e => e.Season)
            .ThenInclude(s => s.TvShow)
            .SingleAsync(e => e.Id == episode.Id);

        Assert.Equal("Pilot", persistedEpisode.Name);
        Assert.Equal(tvShow.Id, persistedEpisode.Season.TvShowId);
        Assert.Equal(season.Id, persistedEpisode.SeasonId);
    }

    [Fact]
    public async Task MovieGenreRelationshipPersistsWithoutDeletingGenre()
    {
        await using var context = CatalogPersistenceFixture.CreateContext();

        var genre = new Genre
        {
            Id = Guid.NewGuid(),
            Name = $"Sci-Fi-{Guid.NewGuid():N}",
            CreatedAt = DateTime.UtcNow
        };

        var movie = CreateMovie(tmdbId: 5001);
        var movieGenre = new MovieGenre
        {
            MovieId = movie.Id,
            GenreId = genre.Id,
            Movie = movie,
            Genre = genre
        };

        context.Genres.Add(genre);
        context.Movies.Add(movie);
        context.MovieGenres.Add(movieGenre);
        await context.SaveChangesAsync();

        var persistedGenreCount = await context.Genres.CountAsync(g => g.Id == genre.Id);
        var persistedMovieGenre = await context.MovieGenres
            .SingleAsync(mg => mg.MovieId == movie.Id && mg.GenreId == genre.Id);

        Assert.Equal(1, persistedGenreCount);
        Assert.Equal(movie.Id, persistedMovieGenre.MovieId);
        Assert.Equal(genre.Id, persistedMovieGenre.GenreId);
    }

    [Fact]
    public async Task DuplicateMovieExternalIdIsRejected()
    {
        await using var context = CatalogPersistenceFixture.CreateContext();

        context.Movies.Add(CreateMovie(tmdbId: 9001));
        await context.SaveChangesAsync();

        context.Movies.Add(CreateMovie(tmdbId: 9001));

        await Assert.ThrowsAsync<DbUpdateException>(() => context.SaveChangesAsync());
    }

    private static Movie CreateMovie(int tmdbId)
    {
        var utcNow = DateTime.UtcNow;

        return new Movie
        {
            Id = Guid.NewGuid(),
            TmdbId = tmdbId,
            Title = "Inception",
            OriginalTitle = "Inception",
            Overview = "A mind-bending thriller.",
            ReleaseDate = new DateOnly(2010, 7, 16),
            RuntimeMinutes = 148,
            OriginalLanguage = "en",
            VoteAverage = 8.4m,
            VoteCount = 30000,
            CreatedAt = utcNow,
            UpdatedAt = utcNow
        };
    }

    private static TvShow CreateTvShow(int tmdbId)
    {
        var utcNow = DateTime.UtcNow;

        return new TvShow
        {
            Id = Guid.NewGuid(),
            TmdbId = tmdbId,
            Title = "Sample Show",
            Overview = "A serialized drama.",
            FirstAirDate = new DateOnly(2020, 1, 1),
            Status = TvShowStatus.ReturningSeries,
            VoteAverage = 7.5m,
            VoteCount = 1200,
            CreatedAt = utcNow,
            UpdatedAt = utcNow
        };
    }

    private static Season CreateSeason(TvShow tvShow, int seasonNumber, int tmdbId)
    {
        var utcNow = DateTime.UtcNow;

        return new Season
        {
            Id = Guid.NewGuid(),
            TvShowId = tvShow.Id,
            TvShow = tvShow,
            TmdbId = tmdbId,
            SeasonNumber = seasonNumber,
            Name = "Season 1",
            EpisodeCount = 10,
            CreatedAt = utcNow,
            UpdatedAt = utcNow
        };
    }

    private static Episode CreateEpisode(Season season, int episodeNumber, int tmdbId)
    {
        var utcNow = DateTime.UtcNow;

        return new Episode
        {
            Id = Guid.NewGuid(),
            SeasonId = season.Id,
            Season = season,
            TmdbId = tmdbId,
            EpisodeNumber = episodeNumber,
            Name = "Pilot",
            AirDate = new DateOnly(2020, 1, 1),
            RuntimeMinutes = 45,
            VoteAverage = 8.0m,
            VoteCount = 100,
            CreatedAt = utcNow,
            UpdatedAt = utcNow
        };
    }
}
