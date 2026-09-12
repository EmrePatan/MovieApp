using MovieApp.Infrastructure.Providers;
using MovieApp.Infrastructure.Providers.Tmdb.TmdbMapping;
using MovieApp.Infrastructure.Providers.Tmdb.TmdbModels;

namespace MovieApp.UnitTests.Providers.Tmdb;

public sealed class TmdbTvShowMapperTests
{
    [Fact]
    public void ToSummaryMapsSearchResultFields()
    {
        var result = TmdbTvShowMapper.ToSummary(new TmdbTvSearchResultJson
        {
            Id = 1399,
            Name = "Game of Thrones",
            OriginalName = "Game of Thrones",
            Overview = "Overview",
            FirstAirDate = "2011-04-17",
            PosterPath = "/poster.jpg",
            VoteAverage = 8.3m,
            VoteCount = 21000
        });

        Assert.Equal("tmdb-1399", result.ExternalId);
        Assert.Equal(1399, result.TmdbId);
        Assert.Equal("Game of Thrones", result.Title);
        Assert.Equal(new DateOnly(2011, 4, 17), result.FirstAirDate);
    }

    [Fact]
    public void ToDetailsMapsTvShowDetailsAndExternalIds()
    {
        var details = TmdbTvShowMapper.ToDetails(new TmdbTvDetailsResponseJson
        {
            Id = 1399,
            Name = "Game of Thrones",
            OriginalName = "Game of Thrones",
            Overview = "Overview",
            FirstAirDate = "2011-04-17",
            LastAirDate = "2019-05-19",
            Status = "Ended",
            PosterPath = "/poster.jpg",
            VoteAverage = 8.3m,
            VoteCount = 21000,
            Genres =
            [
                new TmdbGenreJson { Id = 18, Name = "Drama" }
            ],
            Seasons =
            [
                new TmdbTvSeasonSummaryJson
                {
                    Id = 3627,
                    Name = "Season 1",
                    SeasonNumber = 1,
                    EpisodeCount = 10,
                    AirDate = "2011-04-17",
                    PosterPath = "/season1.jpg"
                },
                new TmdbTvSeasonSummaryJson
                {
                    Id = 0,
                    Name = "Specials",
                    SeasonNumber = 0,
                    EpisodeCount = 1
                }
            ],
            ExternalIds = new TmdbExternalIdsJson
            {
                ImdbId = "tt0944947",
                TvdbId = 121361
            }
        });

        Assert.Equal("tt0944947", details.ImdbId);
        Assert.Equal(121361, details.TvdbId);
        Assert.Equal("Ended", details.Status);
        Assert.Single(details.Seasons);
        Assert.Equal(1, details.Seasons[0].SeasonNumber);
    }

    [Fact]
    public void ToSeasonDetailsMapsEpisodes()
    {
        var season = TmdbTvShowMapper.ToSeasonDetails(
            "tmdb-1399",
            new TmdbTvSeasonDetailsResponseJson
            {
                Id = 3627,
                Name = "Season 1",
                SeasonNumber = 1,
                EpisodeCount = 1,
                Episodes =
                [
                    new TmdbTvEpisodeJson
                    {
                        Id = 63056,
                        Name = "Winter Is Coming",
                        EpisodeNumber = 1,
                        SeasonNumber = 1,
                        Runtime = 62,
                        VoteAverage = 8.0m,
                        VoteCount = 100
                    }
                ]
            });

        Assert.Equal("tmdb-1399", season.ExternalTvShowId);
        Assert.Single(season.Episodes);
        Assert.Equal(62, season.Episodes[0].RuntimeMinutes);
    }
}
