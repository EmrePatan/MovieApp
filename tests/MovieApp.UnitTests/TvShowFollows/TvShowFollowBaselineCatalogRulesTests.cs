using MovieApp.Application.Services.TvShowFollows;
using MovieApp.Domain.Entities;

namespace MovieApp.UnitTests.TvShowFollows;

public sealed class TvShowFollowBaselineCatalogRulesTests
{
    private static readonly DateOnly Boundary = new(2026, 9, 15);
    private static readonly DateOnly Past = new(2026, 8, 1);
    private static readonly DateOnly Future = new(2026, 10, 1);

    [Fact]
    public void NeedsSeasonSummaries_ReturnsTrueWhenNoRegularSeasonsExist()
    {
        Assert.True(TvShowFollowBaselineCatalogRules.NeedsSeasonSummaries(
            [CreateSeason(0, Past, 3)]));
    }

    [Fact]
    public void NeedsEpisodeHydration_ReturnsTrueForHistoricalSeasonWithZeroEpisodes()
    {
        var season = CreateSeason(1, Past, 10);
        Assert.True(TvShowFollowBaselineCatalogRules.NeedsEpisodeHydration(season, 0, Boundary, []));
    }

    [Fact]
    public void NeedsEpisodeHydration_ReturnsTrueForPartialHistoricalSeason()
    {
        var season = CreateSeason(1, Past, 10, CreateEpisode(1, Past));
        var episodes = season.Episodes.Where(episode => episode.EpisodeNumber >= 1).ToList();

        Assert.True(TvShowFollowBaselineCatalogRules.NeedsEpisodeHydration(
            season,
            episodes.Count,
            Boundary,
            episodes));
    }

    [Fact]
    public void NeedsEpisodeHydration_ReturnsFalseForFutureSeason()
    {
        var season = CreateSeason(2, Future, 10);
        Assert.False(TvShowFollowBaselineCatalogRules.NeedsEpisodeHydration(season, 0, Boundary, []));
    }

    [Fact]
    public void NeedsEpisodeHydration_ReturnsFalseForSeasonZero()
    {
        var season = CreateSeason(0, Past, 10);
        Assert.False(TvShowFollowBaselineCatalogRules.NeedsEpisodeHydration(season, 0, Boundary, []));
    }

    [Fact]
    public void NeedsEpisodeHydration_ReturnsFalseWhenPersistedEpisodesMeetEpisodeCount()
    {
        var season = CreateSeason(
            1,
            Past,
            2,
            CreateEpisode(1, Past),
            CreateEpisode(2, Past));

        var episodes = season.Episodes.Where(episode => episode.EpisodeNumber >= 1).ToList();

        Assert.False(TvShowFollowBaselineCatalogRules.NeedsEpisodeHydration(
            season,
            episodes.Count,
            Boundary,
            episodes));
    }

    [Fact]
    public void IsReleaseRelevant_UsesPersistedEpisodeAirDateWhenSeasonAirDateNull()
    {
        var season = CreateSeason(1, null, 10, CreateEpisode(1, Past));
        var episodes = season.Episodes.Where(episode => episode.EpisodeNumber >= 1).ToList();

        Assert.True(TvShowFollowBaselineCatalogRules.IsReleaseRelevant(season, Boundary, episodes));
    }

    [Fact]
    public void IsReleaseRelevant_ReturnsFalseForNullAirDateSeasonWithoutReleasedEpisodes()
    {
        var season = CreateSeason(1, null, 10);
        Assert.False(TvShowFollowBaselineCatalogRules.IsReleaseRelevant(season, Boundary, []));
    }

    private static Season CreateSeason(
        int seasonNumber,
        DateOnly? airDate,
        int? episodeCount,
        params Episode[] episodes) =>
        new()
        {
            Id = Guid.NewGuid(),
            SeasonNumber = seasonNumber,
            AirDate = airDate,
            EpisodeCount = episodeCount,
            Episodes = episodes.ToList()
        };

    private static Episode CreateEpisode(int episodeNumber, DateOnly airDate) =>
        new()
        {
            Id = Guid.NewGuid(),
            EpisodeNumber = episodeNumber,
            AirDate = airDate
        };
}
