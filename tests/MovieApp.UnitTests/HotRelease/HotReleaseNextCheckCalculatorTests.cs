using MovieApp.Application.Services.HotRelease;
using MovieApp.Domain.Entities;
using MovieApp.Domain.Notifications;

namespace MovieApp.UnitTests.HotRelease;

public sealed class HotReleaseNextCheckCalculatorTests
{
    private static readonly DateOnly Boundary = new(2026, 9, 14);

    [Fact]
    public void ComputeNextHotCheckAtUtc_UsesEarliestFutureRegularRelease()
    {
        var seasons = new[]
        {
            CreateSeason(1, episodes: CreateEpisode(5, new DateOnly(2026, 9, 18))),
            CreateSeason(2, episodes: CreateEpisode(1, new DateOnly(2026, 9, 20)))
        };

        var nextCheck = HotReleaseNextCheckCalculator.ComputeNextHotCheckAtUtc(seasons, Boundary);

        Assert.Equal(ReleaseDateTime.ToReleaseAtUtc(new DateOnly(2026, 9, 18)), nextCheck);
    }

    [Fact]
    public void ComputeNextHotCheckAtUtc_ReturnsNullWhenNoFutureReleaseExists()
    {
        var seasons = new[]
        {
            CreateSeason(1, episodes: CreateEpisode(1, new DateOnly(2026, 9, 10)))
        };

        var nextCheck = HotReleaseNextCheckCalculator.ComputeNextHotCheckAtUtc(seasons, Boundary);

        Assert.Null(nextCheck);
    }

    [Fact]
    public void ComputeNextHotCheckAtUtc_IgnoresSeasonZero()
    {
        var seasons = new[]
        {
            CreateSeason(0, airDate: new DateOnly(2026, 9, 18), episodes: CreateEpisode(1, new DateOnly(2026, 9, 18)))
        };

        var nextCheck = HotReleaseNextCheckCalculator.ComputeNextHotCheckAtUtc(seasons, Boundary);

        Assert.Null(nextCheck);
    }

    private static Season CreateSeason(
        int seasonNumber,
        DateOnly? airDate = null,
        int? episodeCount = null,
        params Episode[] episodes)
    {
        return new Season
        {
            Id = Guid.NewGuid(),
            TvShowId = Guid.NewGuid(),
            SeasonNumber = seasonNumber,
            AirDate = airDate,
            EpisodeCount = episodeCount,
            Episodes = episodes.ToList()
        };
    }

    private static Episode CreateEpisode(int episodeNumber, DateOnly? airDate) =>
        new()
        {
            Id = Guid.NewGuid(),
            SeasonId = Guid.NewGuid(),
            EpisodeNumber = episodeNumber,
            AirDate = airDate
        };
}
