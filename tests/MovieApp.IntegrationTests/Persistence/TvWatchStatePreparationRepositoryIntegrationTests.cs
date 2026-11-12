using MovieApp.Domain.Entities;
using MovieApp.Domain.Enums;
using MovieApp.Infrastructure.Persistence;
using MovieApp.Infrastructure.Persistence.Repositories;

namespace MovieApp.IntegrationTests.Persistence;

[Collection("CatalogPersistence")]
public sealed class TvWatchStatePreparationRepositoryIntegrationTests
{
    [Fact]
    public async Task PrepareAsyncIncludesOnlyEpisodesWithKnownAirDateOnOrBeforeToday()
    {
        await using var context = CatalogPersistenceFixture.CreateContext();
        var now = DateTime.UtcNow;
        var tvShowId = Guid.NewGuid();
        var seasonId = Guid.NewGuid();
        var releasedEpisodeId = Guid.NewGuid();

        context.TvShows.Add(new TvShow
        {
            Id = tvShowId,
            TmdbId = Random.Shared.Next(1_000_000, 9_999_999),
            Title = $"Watch preparation {Guid.NewGuid():N}",
            Status = TvShowStatus.ReturningSeries,
            CreatedAt = now,
            UpdatedAt = now
        });
        context.Seasons.Add(new Season
        {
            Id = seasonId,
            TvShowId = tvShowId,
            SeasonNumber = 1,
            EpisodeCount = 3,
            CreatedAt = now,
            UpdatedAt = now
        });
        context.Episodes.AddRange(
            new Episode
            {
                Id = releasedEpisodeId,
                SeasonId = seasonId,
                EpisodeNumber = 1,
                Name = "Released",
                AirDate = new DateOnly(2000, 1, 1),
                CreatedAt = now,
                UpdatedAt = now
            },
            new Episode
            {
                Id = Guid.NewGuid(),
                SeasonId = seasonId,
                EpisodeNumber = 2,
                Name = "Future",
                AirDate = new DateOnly(2099, 1, 1),
                CreatedAt = now,
                UpdatedAt = now
            },
            new Episode
            {
                Id = Guid.NewGuid(),
                SeasonId = seasonId,
                EpisodeNumber = 3,
                Name = "Unknown air date",
                AirDate = null,
                CreatedAt = now,
                UpdatedAt = now
            });

        await context.SaveChangesAsync();

        var preparation = await new TvWatchStatePreparationRepository(context).PrepareAsync(tvShowId);

        Assert.True(preparation.TvShowExists);
        Assert.Equal([releasedEpisodeId], preparation.EpisodeIds);
        Assert.False(preparation.IngestionRequired);
    }
}
