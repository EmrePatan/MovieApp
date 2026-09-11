using Microsoft.EntityFrameworkCore;
using MovieApp.Application.Models.Providers;
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
        var repository = new TvShowRepository(context);

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
        var repository = new TvShowRepository(context);
        var details = CreateBreakingBadDetails();

        await repository.UpsertFromProviderAsync(details);
        await repository.UpsertFromProviderAsync(details);

        Assert.Equal(3, await context.TvShowGenres.CountAsync());
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
