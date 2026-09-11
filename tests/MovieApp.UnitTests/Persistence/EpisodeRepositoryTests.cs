using Microsoft.EntityFrameworkCore;
using MovieApp.Application.Models.Providers;
using MovieApp.Domain.Entities;
using MovieApp.Infrastructure.Persistence;
using MovieApp.Infrastructure.Persistence.Repositories;

namespace MovieApp.UnitTests.Persistence;

public sealed class EpisodeRepositoryTests
{
    [Fact]
    public async Task UpsertFromProviderAsyncPreservesEpisodeIdForDuplicateExternalData()
    {
        var options = new DbContextOptionsBuilder<ApplicationDbContext>()
            .UseInMemoryDatabase($"episode-repository-{Guid.NewGuid()}")
            .Options;

        await using var context = new ApplicationDbContext(options);
        var seasonId = await SeedSeasonAsync(context);
        var repository = new EpisodeRepository(context);
        var details = CreateEpisodeDetails();

        var created = await repository.UpsertFromProviderAsync(seasonId, details);
        var updated = await repository.UpsertFromProviderAsync(
            seasonId,
            details with { VoteCount = 250, Overview = "Updated overview" });

        Assert.Equal(created.Id, updated.Id);
        Assert.Equal(250, updated.VoteCount);
        Assert.Equal(1, await context.Episodes.CountAsync());
    }

    private static async Task<Guid> SeedSeasonAsync(ApplicationDbContext context)
    {
        var tvShow = new TvShow
        {
            Id = Guid.NewGuid(),
            TmdbId = 900101,
            Title = "Breaking Bad",
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow
        };

        var season = new Season
        {
            Id = Guid.NewGuid(),
            TvShowId = tvShow.Id,
            TvShow = tvShow,
            SeasonNumber = 1,
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow
        };

        context.TvShows.Add(tvShow);
        context.Seasons.Add(season);
        await context.SaveChangesAsync();
        return season.Id;
    }

    private static EpisodeProviderDetails CreateEpisodeDetails() =>
        new(
            "fake-tv-900101",
            TmdbId: 900301,
            TvdbId: null,
            ImdbId: "tt900301",
            SeasonNumber: 1,
            EpisodeNumber: 1,
            Name: "Pilot",
            Overview: "Pilot overview",
            AirDate: new DateOnly(2008, 1, 20),
            RuntimeMinutes: 58,
            StillPath: "/fake/s1e1.jpg",
            VoteAverage: 8.2m,
            VoteCount: 100);
}
