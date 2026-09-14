using MovieApp.Domain.Entities;
using MovieApp.Domain.Enums;
using MovieApp.Infrastructure.Persistence;
using MovieApp.Infrastructure.Persistence.Repositories;

namespace MovieApp.IntegrationTests.Persistence;

[Collection("CatalogPersistence")]
public sealed class EpisodeRepositoryIntegrationTests
{
    [Fact]
    public async Task GetEpisodeCountsBySeasonAsyncTranslatesAgainstPostgreSql()
    {
        await using var context = CatalogPersistenceFixture.CreateContext();
        var tvShowId = await SeedTvShowAsync(
            context,
            (seasonNumber: 0, episodeCount: 2),
            (seasonNumber: 1, episodeCount: 8),
            (seasonNumber: 2, episodeCount: 13));
        var repository = new EpisodeRepository(context);

        var counts = await repository.GetEpisodeCountsBySeasonAsync(tvShowId);

        Assert.Equal(3, counts.Count);
        Assert.Equal([0, 1, 2], counts.Select(count => count.SeasonNumber).ToList());
        Assert.Equal(2, counts[0].EpisodeCount);
        Assert.Equal(8, counts[1].EpisodeCount);
        Assert.Equal(13, counts[2].EpisodeCount);
    }

    [Fact]
    public async Task GetFirstUnwatchedForTvShowAsyncIgnoresSeasonZeroAgainstPostgreSql()
    {
        await using var context = CatalogPersistenceFixture.CreateContext();
        var userId = Guid.NewGuid();
        var tvShowId = await SeedTvShowAsync(
            context,
            (seasonNumber: 0, episodeCount: 2),
            (seasonNumber: 1, episodeCount: 3));
        var repository = new EpisodeRepository(context);

        var nextEpisode = await repository.GetFirstUnwatchedForTvShowAsync(tvShowId, userId);

        Assert.NotNull(nextEpisode);
        Assert.Equal(1, nextEpisode!.Season.SeasonNumber);
        Assert.Equal(1, nextEpisode.EpisodeNumber);
    }

    [Fact]
    public async Task GetEpisodeCountsBySeasonAsyncReturnsEmptyListForShowWithoutEpisodes()
    {
        await using var context = CatalogPersistenceFixture.CreateContext();
        var tvShowId = await SeedTvShowAsync(context);
        var repository = new EpisodeRepository(context);

        var counts = await repository.GetEpisodeCountsBySeasonAsync(tvShowId);

        Assert.Empty(counts);
    }

    private static async Task<Guid> SeedTvShowAsync(
        ApplicationDbContext context,
        params (int seasonNumber, int episodeCount)[] seasons)
    {
        var utcNow = DateTime.UtcNow;
        var tvShow = new TvShow
        {
            Id = Guid.NewGuid(),
            TmdbId = Random.Shared.Next(1_000_000, 9_999_999),
            Title = $"Repository Show {Guid.NewGuid():N}",
            Status = TvShowStatus.Ended,
            CreatedAt = utcNow,
            UpdatedAt = utcNow,
        };

        context.TvShows.Add(tvShow);

        foreach (var (seasonNumber, episodeCount) in seasons)
        {
            var season = new Season
            {
                Id = Guid.NewGuid(),
                TvShowId = tvShow.Id,
                TvShow = tvShow,
                SeasonNumber = seasonNumber,
                CreatedAt = utcNow,
                UpdatedAt = utcNow,
            };

            context.Seasons.Add(season);

            for (var episodeNumber = 1; episodeNumber <= episodeCount; episodeNumber++)
            {
                context.Episodes.Add(new Episode
                {
                    Id = Guid.NewGuid(),
                    SeasonId = season.Id,
                    Season = season,
                    EpisodeNumber = episodeNumber,
                    Name = $"Episode {episodeNumber}",
                    CreatedAt = utcNow,
                    UpdatedAt = utcNow,
                });
            }
        }

        await context.SaveChangesAsync();
        return tvShow.Id;
    }
}
