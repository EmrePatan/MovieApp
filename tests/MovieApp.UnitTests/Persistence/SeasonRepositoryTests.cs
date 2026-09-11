using Microsoft.EntityFrameworkCore;
using MovieApp.Application.Models.Providers;
using MovieApp.Domain.Entities;
using MovieApp.Infrastructure.Persistence;
using MovieApp.Infrastructure.Persistence.Repositories;

namespace MovieApp.UnitTests.Persistence;

public sealed class SeasonRepositoryTests
{
    [Fact]
    public async Task UpsertFromProviderAsyncPreservesSeasonIdAndPreventsDuplicateEpisodes()
    {
        var options = new DbContextOptionsBuilder<ApplicationDbContext>()
            .UseInMemoryDatabase($"season-repository-{Guid.NewGuid()}")
            .Options;

        await using var context = new ApplicationDbContext(options);
        var tvShowId = await SeedTvShowAsync(context);
        var repository = new SeasonRepository(context);
        var details = CreateSeasonDetails();

        var created = await repository.UpsertFromProviderAsync(tvShowId, details);
        var updated = await repository.UpsertFromProviderAsync(
            tvShowId,
            details with { Overview = "Updated overview" });

        Assert.Equal(created.Id, updated.Id);
        Assert.Equal(1, await context.Seasons.CountAsync());
        Assert.Equal(2, await context.Episodes.CountAsync());
    }

    private static async Task<Guid> SeedTvShowAsync(ApplicationDbContext context)
    {
        var tvShow = new TvShow
        {
            Id = Guid.NewGuid(),
            TmdbId = 900101,
            Title = "Breaking Bad",
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow
        };

        context.TvShows.Add(tvShow);
        await context.SaveChangesAsync();
        return tvShow.Id;
    }

    private static SeasonProviderDetails CreateSeasonDetails() =>
        new(
            "fake-tv-900101",
            TmdbId: 900201,
            TvdbId: null,
            SeasonNumber: 1,
            Name: "Season 1",
            Overview: "Overview",
            AirDate: new DateOnly(2008, 1, 20),
            EpisodeCount: 2,
            PosterPath: "/fake/s1.jpg",
            Episodes:
            [
                new EpisodeProviderDetails(
                    "fake-tv-900101", 900301, null, "tt900301", 1, 1, "Pilot", "Pilot overview",
                    new DateOnly(2008, 1, 20), 58, "/fake/s1e1.jpg", 8.2m, 100),
                new EpisodeProviderDetails(
                    "fake-tv-900101", 900302, null, "tt900302", 1, 2, "Episode 2", "Episode 2 overview",
                    new DateOnly(2008, 1, 27), 48, "/fake/s1e2.jpg", 8.1m, 90)
            ]);
}
