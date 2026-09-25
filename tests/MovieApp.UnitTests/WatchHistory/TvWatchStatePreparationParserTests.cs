using MovieApp.Infrastructure.Persistence.Repositories;
using Xunit;

namespace MovieApp.UnitTests.WatchHistory;

public sealed class TvWatchStatePreparationParserTests
{
    [Fact]
    public void ParseRegularSeasons_mapsPostgresSnakeCaseJson()
    {
        const string json =
            """[{"season_number":1,"episode_count":8,"has_episodes":true},{"season_number":2,"episode_count":10,"has_episodes":false}]""";

        var seasons = TvWatchStatePreparationParser.ParseRegularSeasons(json);

        Assert.Equal(2, seasons.Count);
        Assert.Equal(1, seasons[0].SeasonNumber);
        Assert.Equal(8, seasons[0].EpisodeCount);
        Assert.True(seasons[0].HasEpisodes);
        Assert.Equal(2, seasons[1].SeasonNumber);
        Assert.False(seasons[1].HasEpisodes);
    }

    [Fact]
    public void EvaluateIngestion_skipsIngestionWhenRegularSeasonsHaveEpisodeRows()
    {
        var seasons = TvWatchStatePreparationParser.ParseRegularSeasons(
            """[{"season_number":1,"episode_count":8,"has_episodes":true}]""");

        var (ingestionRequired, missing, withRows) = TvWatchStatePreparationParser.EvaluateIngestion(seasons);

        Assert.False(ingestionRequired);
        Assert.Empty(missing);
        Assert.Equal(1, withRows);
    }

    [Fact]
    public void EvaluateIngestion_requiresIngestionForMissingRegularSeasonEpisodes()
    {
        var seasons = TvWatchStatePreparationParser.ParseRegularSeasons(
            """[{"season_number":3,"episode_count":12,"has_episodes":false}]""");

        var (ingestionRequired, missing, _) = TvWatchStatePreparationParser.EvaluateIngestion(seasons);

        Assert.True(ingestionRequired);
        Assert.Equal([3], missing);
    }

    [Fact]
    public void EvaluateIngestion_doesNotTreatUnmappedSeasonNumberZeroAsMissingRegularSeason()
    {
        // Regression: default SeasonNumber=0 when JSON names were not mapped caused MissingSeasonNumbers=0.
        var seasons = TvWatchStatePreparationParser.ParseRegularSeasons(
            """[{"episode_count":8,"has_episodes":true}]""");

        Assert.Equal(0, seasons[0].SeasonNumber);

        var (ingestionRequired, missing, _) = TvWatchStatePreparationParser.EvaluateIngestion(seasons);

        Assert.False(ingestionRequired);
        Assert.Empty(missing);
    }

    [Fact]
    public void EvaluateIngestion_ignoresZeroEpisodeCountSeasons()
    {
        var seasons = TvWatchStatePreparationParser.ParseRegularSeasons(
            """[{"season_number":4,"episode_count":0,"has_episodes":false}]""");

        var (ingestionRequired, missing, _) = TvWatchStatePreparationParser.EvaluateIngestion(seasons);

        Assert.False(ingestionRequired);
        Assert.Empty(missing);
    }

    [Fact]
    public void EvaluateIngestion_requiresIngestionWhenNoRegularSeasonRowsExist()
    {
        var (ingestionRequired, missing, withRows) = TvWatchStatePreparationParser.EvaluateIngestion([]);

        Assert.True(ingestionRequired);
        Assert.Empty(missing);
        Assert.Equal(0, withRows);
    }
}
