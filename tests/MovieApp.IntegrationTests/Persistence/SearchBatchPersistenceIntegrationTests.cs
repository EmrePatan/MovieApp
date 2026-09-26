using Microsoft.EntityFrameworkCore;
using MovieApp.Application.Exceptions;
using MovieApp.Application.Models.Providers;
using MovieApp.Domain.Entities;
using CatalogSyncState = MovieApp.Domain.Entities.TvShowCatalogSyncState;
using MovieApp.Domain.Enums;
using MovieApp.Infrastructure.Persistence;
using MovieApp.Infrastructure.Persistence.Repositories;

namespace MovieApp.IntegrationTests.Persistence;

[Collection("CatalogPersistence")]
public sealed class SearchBatchPersistenceIntegrationTests
{
    [Fact]
    public async Task MovieBatchUpsertPersistsMultipleNewMoviesInSingleSaveAgainstPostgreSql()
    {
        await using var context = CatalogPersistenceFixture.CreateContext();
        await ClearMovieCatalogAsync(context);
        var repository = CatalogRepositoryTestFactory.CreateMovieRepository(context);

        var details = new[]
        {
            CreateMovieDetails(910001, "tt9100001", "Batch Movie One", ["Drama"]),
            CreateMovieDetails(910002, "tt9100002", "Batch Movie Two", ["Comedy"]),
            CreateMovieDetails(910003, "tt9100003", "Batch Movie Three", ["Action"])
        };

        var movies = await repository.UpsertFromProviderBatchAsync(details);

        Assert.Equal(3, movies.Count);
        var movieIds = movies.Select(movie => movie.Id).ToList();
        Assert.Equal(3, await context.Movies.CountAsync(movie => movieIds.Contains(movie.Id)));
        Assert.Equal(3, await context.MovieGenres.CountAsync(link => movieIds.Contains(link.MovieId)));
    }

    [Fact]
    public async Task MovieBatchUpsertUpdatesExistingAndInsertsNewMoviesAgainstPostgreSql()
    {
        await using var context = CatalogPersistenceFixture.CreateContext();
        await ClearMovieCatalogAsync(context);

        var existingId = Guid.NewGuid();
        var utcNow = DateTime.UtcNow;
        context.Movies.Add(new Movie
        {
            Id = existingId,
            TmdbId = 920001,
            ImdbId = "tt9200001",
            Title = "Existing Batch Movie",
            VoteCount = 10,
            CreatedAt = utcNow,
            UpdatedAt = utcNow
        });
        await context.SaveChangesAsync();

        var repository = CatalogRepositoryTestFactory.CreateMovieRepository(context);
        var movies = await repository.UpsertFromProviderBatchAsync(
        [
            CreateMovieDetails(920001, "tt9200001", "Updated Batch Movie", ["Drama", "Sci-Fi"]),
            CreateMovieDetails(920002, "tt9200002", "New Batch Movie", ["Thriller"])
        ]);

        Assert.Equal(2, movies.Count);
        var movieIds = movies.Select(movie => movie.Id).ToList();
        Assert.Equal(2, await context.Movies.CountAsync(movie => movieIds.Contains(movie.Id)));
        Assert.Equal(existingId, movies[0].Id);
        Assert.Equal("Updated Batch Movie", movies[0].Title);
        Assert.Equal(100, movies[0].VoteCount);
        Assert.Equal(3, await context.MovieGenres.CountAsync(link => movieIds.Contains(link.MovieId)));
    }

    [Fact]
    public async Task MovieBatchUpsertReusesSharedGenresAcrossMoviesAgainstPostgreSql()
    {
        await using var context = CatalogPersistenceFixture.CreateContext();
        await ClearMovieCatalogAsync(context);
        var repository = CatalogRepositoryTestFactory.CreateMovieRepository(context);

        var movies = await repository.UpsertFromProviderBatchAsync(
        [
            CreateMovieDetails(930001, "tt9300001", "Shared Genre Movie A", ["Sci-Fi", "Drama"]),
            CreateMovieDetails(930002, "tt9300002", "Shared Genre Movie B", ["Sci-Fi", "Comedy"])
        ]);
        var movieIds = movies.Select(movie => movie.Id).ToList();

        Assert.Equal(2, await context.Movies.CountAsync(movie => movieIds.Contains(movie.Id)));
        Assert.Equal(4, await context.MovieGenres.CountAsync(link => movieIds.Contains(link.MovieId)));
        var linkedGenreIds = await context.MovieGenres
            .Where(link => movieIds.Contains(link.MovieId))
            .Select(link => link.GenreId)
            .Distinct()
            .CountAsync();
        Assert.Equal(3, linkedGenreIds);
        var sciFiLinks = await context.MovieGenres
            .Where(link => movieIds.Contains(link.MovieId))
            .Join(
                context.Genres,
                link => link.GenreId,
                genre => genre.Id,
                (link, genre) => new { link, genre })
            .Where(joined => joined.genre.Name == "Sci-Fi")
            .ToListAsync();
        Assert.Equal(2, sciFiLinks.Count);
        Assert.Single(sciFiLinks.Select(joined => joined.genre.Id).Distinct());
    }

    [Fact]
    public async Task MovieBatchUpsertDoesNotCreateDuplicateRowsForSameTmdbIdAgainstPostgreSql()
    {
        await using var context = CatalogPersistenceFixture.CreateContext();
        await ClearMovieCatalogAsync(context);
        var repository = CatalogRepositoryTestFactory.CreateMovieRepository(context);

        var duplicateDetails = CreateMovieDetails(940001, "tt9400001", "Duplicate Tmdb Movie", ["Drama"]);
        await repository.UpsertFromProviderBatchAsync([duplicateDetails, duplicateDetails with { Title = "Duplicate Tmdb Movie Updated" }]);

        Assert.Equal(1, await context.Movies.CountAsync(movie => movie.TmdbId == 940001));
        Assert.Equal(
            "Duplicate Tmdb Movie Updated",
            (await context.Movies.SingleAsync(movie => movie.TmdbId == 940001)).Title);
    }

    [Fact]
    public async Task MovieBatchConflictFallbackPreservesCommittedDataAndSkipsConflictingItemAgainstPostgreSql()
    {
        var seededMovieId = Guid.NewGuid();
        var utcNow = DateTime.UtcNow;

        await using (var seedContext = CatalogPersistenceFixture.CreateContext())
        {
            seedContext.Movies.Add(new Movie
            {
                Id = seededMovieId,
                TmdbId = 950000,
                ImdbId = "tt9500000",
                Title = "Seeded Conflict Movie",
                CreatedAt = utcNow,
                UpdatedAt = utcNow
            });
            await seedContext.SaveChangesAsync();
        }

        await using var context = CatalogPersistenceFixture.CreateContext();
        var repository = CatalogRepositoryTestFactory.CreateMovieRepository(context);
        var details = new[]
        {
            CreateMovieDetails(950001, "tt9500001", "Good Batch Movie", ["Drama"]),
            CreateMovieDetails(950002, "tt9500000", "Conflicting Batch Movie", ["Comedy"])
        };
        await Assert.ThrowsAsync<MovieExternalIdPersistenceConflictException>(() =>
            repository.UpsertFromProviderBatchAsync(details));

        var persistedIds = new List<Guid>();
        for (var index = 0; index < details.Length; index++)
        {
            try
            {
                var movie = await repository.UpsertFromProviderAsync(details[index]);
                persistedIds.Add(movie.Id);
            }
            catch (MovieExternalIdPersistenceConflictException)
            {
            }
        }

        Assert.Single(persistedIds);
        Assert.True(await context.Movies.AnyAsync(movie => movie.Id == seededMovieId));
        Assert.True(await context.Movies.AnyAsync(movie => movie.TmdbId == 950001));
        Assert.False(await context.Movies.AnyAsync(movie => movie.TmdbId == 950002));
        Assert.Equal(1, await context.Movies.CountAsync(movie => movie.TmdbId == 950001 || movie.TmdbId == 950002));
    }

    [Fact]
    public async Task TvShowBatchUpsertPersistsMultipleNewShowsWithSeasonsAgainstPostgreSql()
    {
        await using var context = CatalogPersistenceFixture.CreateContext();
        await ClearTvCatalogAsync(context);
        var repository = CatalogRepositoryTestFactory.CreateTvShowRepository(context);

        var tvShows = await repository.UpsertFromProviderBatchAsync(
        [
            CreateTvShowDetails(
                960001,
                "Batch TV One",
                ["Drama"],
                [new SeasonProviderSummary(1, "Season 1", new DateOnly(2020, 1, 1), 8, null)]),
            CreateTvShowDetails(
                960002,
                "Batch TV Two",
                ["Comedy"],
                [new SeasonProviderSummary(1, "Season 1", new DateOnly(2021, 1, 1), 10, null)])
        ]);

        Assert.Equal(2, tvShows.Count);
        var tvShowIds = tvShows.Select(tvShow => tvShow.Id).ToList();
        Assert.Equal(2, await context.TvShows.CountAsync(tvShow => tvShowIds.Contains(tvShow.Id)));
        Assert.Equal(2, await context.Seasons.CountAsync(season => tvShowIds.Contains(season.TvShowId)));
        Assert.Equal(2, await context.TvShowGenres.CountAsync(link => tvShowIds.Contains(link.TvShowId)));
    }

    [Fact]
    public async Task TvShowBatchUpsertUpdatesExistingShowAndSeasonsAgainstPostgreSql()
    {
        await using var context = CatalogPersistenceFixture.CreateContext();
        await ClearTvCatalogAsync(context);

        var tvShowId = Guid.NewGuid();
        var seasonId = Guid.NewGuid();
        var utcNow = DateTime.UtcNow;
        context.TvShows.Add(new TvShow
        {
            Id = tvShowId,
            TmdbId = 970001,
            Title = "Existing Batch Show",
            Status = TvShowStatus.ReturningSeries,
            CreatedAt = utcNow,
            UpdatedAt = utcNow
        });
        context.Seasons.Add(new Season
        {
            Id = seasonId,
            TvShowId = tvShowId,
            SeasonNumber = 1,
            EpisodeCount = 5,
            CreatedAt = utcNow,
            UpdatedAt = utcNow
        });
        await context.SaveChangesAsync();

        var repository = CatalogRepositoryTestFactory.CreateTvShowRepository(context);
        var tvShows = await repository.UpsertFromProviderBatchAsync(
        [
            CreateTvShowDetails(
                970001,
                "Updated Batch Show",
                ["Drama", "Crime"],
                [
                    new SeasonProviderSummary(1, "Season 1", new DateOnly(2020, 1, 1), 8, null),
                    new SeasonProviderSummary(2, "Season 2", new DateOnly(2021, 1, 1), 6, null)
                ])
        ]);

        Assert.Single(tvShows);
        Assert.Equal(tvShowId, tvShows[0].Id);
        Assert.Equal("Updated Batch Show", tvShows[0].Title);
        Assert.Equal(1, await context.TvShows.CountAsync(tvShow => tvShow.Id == tvShowId));
        Assert.Equal(2, await context.Seasons.CountAsync(season => season.TvShowId == tvShowId));
        var seasonOne = await context.Seasons.SingleAsync(season => season.TvShowId == tvShowId && season.SeasonNumber == 1);
        Assert.Equal(8, seasonOne.EpisodeCount);
        Assert.Equal(2, await context.TvShowGenres.CountAsync(link => link.TvShowId == tvShowId));
    }

    [Fact]
    public async Task TvShowBatchUpsertDoesNotCreateDuplicateRowsForSameTmdbIdAgainstPostgreSql()
    {
        await using var context = CatalogPersistenceFixture.CreateContext();
        await ClearTvCatalogAsync(context);
        var repository = CatalogRepositoryTestFactory.CreateTvShowRepository(context);

        var duplicateDetails = CreateTvShowDetails(
            980001,
            "Duplicate Tmdb Show",
            ["Drama"],
            [new SeasonProviderSummary(1, "Season 1", new DateOnly(2020, 1, 1), 8, null)]);

        await repository.UpsertFromProviderBatchAsync([duplicateDetails, duplicateDetails with { Title = "Duplicate Tmdb Show Updated" }]);

        Assert.Equal(1, await context.TvShows.CountAsync(tvShow => tvShow.TmdbId == 980001));
        Assert.Equal(
            "Duplicate Tmdb Show Updated",
            (await context.TvShows.SingleAsync(tvShow => tvShow.TmdbId == 980001)).Title);
    }

    [Fact]
    public async Task MarkRefreshedBatchAsyncCreatesAndUpdatesOnlyIntendedShowsAgainstPostgreSql()
    {
        await using var context = CatalogPersistenceFixture.CreateContext();
        await ClearTvCatalogAsync(context);

        var utcNow = DateTime.UtcNow;
        var showToCreate = Guid.NewGuid();
        var showToUpdate = Guid.NewGuid();
        var unrelatedShow = Guid.NewGuid();
        var newerShow = Guid.NewGuid();

        context.TvShows.AddRange(
            CreateTvShow(showToUpdate, 990001, utcNow),
            CreateTvShow(unrelatedShow, 990002, utcNow),
            CreateTvShow(newerShow, 990003, utcNow),
            CreateTvShow(showToCreate, 990004, utcNow));
        context.TvShowCatalogSyncStates.Add(new CatalogSyncState
        {
            TvShowId = showToUpdate,
            LastRefreshedAtUtc = utcNow.AddDays(-2),
            LastRefreshReason = TvShowCatalogRefreshReason.DetailHydration,
            UpdatedAtUtc = utcNow.AddDays(-2)
        });
        context.TvShowCatalogSyncStates.Add(new CatalogSyncState
        {
            TvShowId = newerShow,
            LastRefreshedAtUtc = utcNow.AddHours(1),
            LastRefreshReason = TvShowCatalogRefreshReason.FollowBaseline,
            UpdatedAtUtc = utcNow.AddHours(1)
        });
        await context.SaveChangesAsync();

        var repository = new TvShowCatalogSyncStateRepository(context);
        var refreshAt = utcNow;

        await repository.MarkRefreshedBatchAsync(
            [showToCreate, showToUpdate, newerShow],
            TvShowCatalogRefreshReason.DetailHydration,
            refreshAt);

        var states = await context.TvShowCatalogSyncStates
            .OrderBy(state => state.TvShowId)
            .ToListAsync();

        Assert.Equal(3, states.Count);

        var createdState = await context.TvShowCatalogSyncStates.SingleAsync(state => state.TvShowId == showToCreate);
        Assert.Equal(refreshAt, createdState.LastRefreshedAtUtc);
        Assert.Equal(TvShowCatalogRefreshReason.DetailHydration, createdState.LastRefreshReason);

        var updatedState = await context.TvShowCatalogSyncStates.SingleAsync(state => state.TvShowId == showToUpdate);
        Assert.Equal(refreshAt, updatedState.LastRefreshedAtUtc);
        Assert.Equal(TvShowCatalogRefreshReason.DetailHydration, updatedState.LastRefreshReason);

        var preservedState = await context.TvShowCatalogSyncStates.SingleAsync(state => state.TvShowId == newerShow);
        Assert.Equal(utcNow.AddHours(1), preservedState.LastRefreshedAtUtc);
        Assert.Equal(TvShowCatalogRefreshReason.FollowBaseline, preservedState.LastRefreshReason);

        Assert.Equal(0, await context.TvShowCatalogSyncStates.CountAsync(state => state.TvShowId == unrelatedShow));
    }

    private static MovieProviderDetails CreateMovieDetails(
        int tmdbId,
        string imdbId,
        string title,
        IReadOnlyList<string> genres) =>
        new(
            ExternalId: $"fake-tmdb-{tmdbId}",
            TmdbId: tmdbId,
            TvdbId: null,
            ImdbId: imdbId,
            Title: title,
            OriginalTitle: title,
            Overview: "Overview",
            ReleaseDate: new DateOnly(2020, 1, 1),
            RuntimeMinutes: 120,
            PosterPath: "/poster.jpg",
            BackdropPath: "/backdrop.jpg",
            OriginalLanguage: "en",
            VoteAverage: 8.0m,
            VoteCount: 100,
            Genres: genres);

    private static TvShowProviderDetails CreateTvShowDetails(
        int tmdbId,
        string title,
        IReadOnlyList<string> genres,
        IReadOnlyList<SeasonProviderSummary> seasons) =>
        new(
            ExternalId: $"fake-tmdb-{tmdbId}",
            TmdbId: tmdbId,
            TvdbId: null,
            ImdbId: null,
            Title: title,
            OriginalTitle: title,
            Overview: "Overview",
            FirstAirDate: new DateOnly(2020, 1, 1),
            LastAirDate: null,
            PosterPath: "/poster.jpg",
            BackdropPath: null,
            OriginalLanguage: "en",
            VoteAverage: 8.5m,
            VoteCount: 200,
            Status: "Returning Series",
            Genres: genres,
            Seasons: seasons);

    private static TvShow CreateTvShow(Guid id, int tmdbId, DateTime utcNow) => new()
    {
        Id = id,
        TmdbId = tmdbId,
        Title = $"Show {tmdbId}",
        Status = TvShowStatus.ReturningSeries,
        CreatedAt = utcNow,
        UpdatedAt = utcNow
    };

    private static async Task ClearMovieCatalogAsync(ApplicationDbContext context)
    {
        context.MovieGenres.RemoveRange(context.MovieGenres);
        context.Movies.RemoveRange(context.Movies);
        await context.SaveChangesAsync();
    }

    private static async Task ClearTvCatalogAsync(ApplicationDbContext context)
    {
        context.TvShowCatalogSyncStates.RemoveRange(context.TvShowCatalogSyncStates);
        context.Seasons.RemoveRange(context.Seasons);
        context.TvShowGenres.RemoveRange(context.TvShowGenres);
        context.TvShows.RemoveRange(context.TvShows);
        await context.SaveChangesAsync();
    }
}
