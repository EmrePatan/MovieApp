using Microsoft.EntityFrameworkCore;
using MovieApp.Application.Models.Catalog;
using MovieApp.Application.Models.Providers;
using MovieApp.Domain.Enums;
using MovieApp.Infrastructure.Persistence;
using MovieApp.Infrastructure.Persistence.Repositories;

namespace MovieApp.UnitTests.Persistence;

public sealed class TvShowRepositoryTests
{
    [Fact]
    public async Task UpsertFromProviderAsyncPreservesInternalIdAndReusesGenres()
    {
        var options = new DbContextOptionsBuilder<ApplicationDbContext>()
            .UseInMemoryDatabase($"tvshow-repository-{Guid.NewGuid()}")
            .Options;

        await using var context = new ApplicationDbContext(options);
        var repository = CatalogRepositoryTestFactory.CreateTvShowRepository(context);

        var details = CreateBreakingBadDetails();

        var created = await repository.UpsertFromProviderAsync(details);
        var updated = await repository.UpsertFromProviderAsync(
            details with { VoteCount = 15000, Overview = "Updated overview" });

        Assert.Equal(created.Id, updated.Id);
        Assert.Equal(15000, updated.VoteCount);
        Assert.Equal(1, await context.TvShows.CountAsync());
        Assert.Equal(3, await context.Genres.CountAsync());
        Assert.Equal(3, await context.TvShowGenres.CountAsync());
        Assert.Equal(3, await context.Seasons.CountAsync());
    }

    [Fact]
    public async Task UpsertFromProviderAsyncDoesNotDuplicateTvShowGenres()
    {
        var options = new DbContextOptionsBuilder<ApplicationDbContext>()
            .UseInMemoryDatabase($"tvshow-repository-{Guid.NewGuid()}")
            .Options;

        await using var context = new ApplicationDbContext(options);
        var repository = CatalogRepositoryTestFactory.CreateTvShowRepository(context);
        var details = CreateBreakingBadDetails();

        await repository.UpsertFromProviderAsync(details);
        await repository.UpsertFromProviderAsync(details);

        Assert.Equal(3, await context.TvShowGenres.CountAsync());
    }

    [Fact]
    public async Task GetExternalIdsByIdAsyncReturnsProviderIds()
    {
        var options = new DbContextOptionsBuilder<ApplicationDbContext>()
            .UseInMemoryDatabase($"tvshow-repository-external-ids-{Guid.NewGuid()}")
            .Options;

        await using var context = new ApplicationDbContext(options);
        var repository = CatalogRepositoryTestFactory.CreateTvShowRepository(context);
        var created = await repository.UpsertFromProviderAsync(CreateBreakingBadDetails());

        var identity = await repository.GetExternalIdsByIdAsync(created.Id);

        Assert.NotNull(identity);
        Assert.Equal(900101, identity.TmdbId);
        Assert.Equal(900102, identity.TvdbId);
        Assert.Equal("tt9003747", identity.ImdbId);
    }

    [Fact]
    public async Task GetStatusAsyncProjectsStatusWithoutRequiringTheShowGraph()
    {
        var options = new DbContextOptionsBuilder<ApplicationDbContext>()
            .UseInMemoryDatabase($"tvshow-repository-status-{Guid.NewGuid()}")
            .Options;

        await using var context = new ApplicationDbContext(options);
        var repository = CatalogRepositoryTestFactory.CreateTvShowRepository(context);
        var created = await repository.UpsertFromProviderAsync(CreateBreakingBadDetails());

        var status = await repository.GetStatusAsync(created.Id);
        var missing = await repository.GetStatusAsync(Guid.NewGuid());

        Assert.Equal(TvShowStatus.Ended, status);
        Assert.Null(missing);
        Assert.True(await repository.ExistsAsync(created.Id));
        Assert.False(await repository.ExistsAsync(Guid.NewGuid()));
    }

    [Fact]
    public async Task EnsureFromSummariesAsyncReusesExistingMovieAppGuid()
    {
        var options = new DbContextOptionsBuilder<ApplicationDbContext>()
            .UseInMemoryDatabase($"tvshow-repository-summary-{Guid.NewGuid()}")
            .Options;

        await using var context = new ApplicationDbContext(options);
        var repository = CatalogRepositoryTestFactory.CreateTvShowRepository(context);

        var existing = await repository.UpsertFromProviderAsync(CreateBreakingBadDetails());
        var resolved = await repository.EnsureFromSummariesAsync(
        [
            new TvShowProviderSummary(
                "fake-tv-900101",
                900101,
                900102,
                "tt9003747",
                "Breaking Bad",
                "Breaking Bad",
                "Overview",
                new DateOnly(2008, 1, 20),
                "/fake/poster.jpg",
                "/fake/backdrop.jpg",
                "en",
                9.5m,
                12000)
        ]);

        Assert.Equal(existing.Id, resolved[900101]);
        Assert.Equal(1, await context.TvShows.CountAsync());
    }

    [Fact]
    public async Task EnsureFromSummariesAsyncCreatesMinimalRowsWithoutSeasonsOrGenres()
    {
        var options = new DbContextOptionsBuilder<ApplicationDbContext>()
            .UseInMemoryDatabase($"tvshow-repository-summary-create-{Guid.NewGuid()}")
            .Options;

        await using var context = new ApplicationDbContext(options);
        var repository = CatalogRepositoryTestFactory.CreateTvShowRepository(context);

        var resolved = await repository.EnsureFromSummariesAsync(
        [
            new TvShowProviderSummary(
                "fake-tv-777777",
                777777,
                null,
                null,
                "New Show",
                null,
                "Overview",
                new DateOnly(2021, 1, 1),
                "/poster.jpg",
                null,
                "en",
                7m,
                20)
        ]);

        Assert.True(resolved.ContainsKey(777777));
        Assert.Equal(1, await context.TvShows.CountAsync());
        Assert.Equal(0, await context.TvShowGenres.CountAsync());
        Assert.Equal(0, await context.Seasons.CountAsync());
    }

    private static TvShowProviderDetails CreateBreakingBadDetails() =>
        new(
            ExternalId: "fake-tv-900101",
            TmdbId: 900101,
            TvdbId: 900102,
            ImdbId: "tt9003747",
            Title: "Breaking Bad",
            OriginalTitle: "Breaking Bad",
            Overview: "Overview",
            FirstAirDate: new DateOnly(2008, 1, 20),
            LastAirDate: new DateOnly(2010, 6, 13),
            PosterPath: "/fake/poster.jpg",
            BackdropPath: "/fake/backdrop.jpg",
            OriginalLanguage: "en",
            VoteAverage: 9.5m,
            VoteCount: 12000,
            Status: "Ended",
            Genres: ["Crime", "Drama", "Thriller"],
            Seasons:
            [
                new SeasonProviderSummary(1, "Season 1", new DateOnly(2008, 1, 20), 3, "/fake/s1.jpg"),
                new SeasonProviderSummary(2, "Season 2", new DateOnly(2009, 3, 8), 2, "/fake/s2.jpg"),
                new SeasonProviderSummary(3, "Season 3", new DateOnly(2010, 3, 21), 2, "/fake/s3.jpg")
            ]);
}

