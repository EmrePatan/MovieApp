using MovieApp.Application.Mapping;
using MovieApp.Domain.Entities;
using MovieApp.Domain.Enums;

namespace MovieApp.UnitTests.Mapping;

public sealed class TvShowMapperTests
{
    [Fact]
    public void ToSearchResultMapsTvShowFields()
    {
        var tvShow = CreateTvShow();

        var result = TvShowMapper.ToSearchResult(tvShow);

        Assert.Equal(tvShow.Id, result.Id);
        Assert.Equal("Breaking Bad", result.Title);
        Assert.Equal("/fake/breaking-bad-poster.jpg", result.PosterPath);
    }

    [Fact]
    public void ToDetailsResultMapsGenresAndSeasons()
    {
        var tvShow = CreateTvShow();
        var season = new Season
        {
            Id = Guid.NewGuid(),
            TvShowId = tvShow.Id,
            SeasonNumber = 1,
            Name = "Season 1",
            AirDate = new DateOnly(2008, 1, 20),
            EpisodeCount = 3,
            PosterPath = "/fake/s1.jpg",
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow
        };
        tvShow.Seasons.Add(season);

        var result = TvShowMapper.ToDetailsResult(tvShow);

        Assert.Equal("Ended", result.Status);
        Assert.Single(result.Genres);
        Assert.Single(result.Seasons);
        Assert.Equal(1, result.Seasons[0].SeasonNumber);
    }

    [Fact]
    public void ToSeasonResultMapsEpisodesInOrder()
    {
        var season = new Season
        {
            Id = Guid.NewGuid(),
            TvShowId = Guid.NewGuid(),
            SeasonNumber = 1,
            Name = "Season 1",
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow,
            Episodes =
            [
                CreateEpisode(2, "Second"),
                CreateEpisode(1, "Pilot")
            ]
        };

        var result = TvShowMapper.ToSeasonResult(season);

        Assert.Equal(2, result.Episodes.Count);
        Assert.Equal(1, result.Episodes[0].EpisodeNumber);
        Assert.Equal("Pilot", result.Episodes[0].Name);
    }

    private static TvShow CreateTvShow()
    {
        var genre = new Genre
        {
            Id = Guid.NewGuid(),
            Name = "Drama",
            CreatedAt = DateTime.UtcNow
        };

        return new TvShow
        {
            Id = Guid.NewGuid(),
            TmdbId = 900101,
            Title = "Breaking Bad",
            OriginalTitle = "Breaking Bad",
            Overview = "Overview",
            FirstAirDate = new DateOnly(2008, 1, 20),
            PosterPath = "/fake/breaking-bad-poster.jpg",
            BackdropPath = "/fake/breaking-bad-backdrop.jpg",
            OriginalLanguage = "en",
            VoteAverage = 9.5m,
            VoteCount = 1000,
            Status = TvShowStatus.Ended,
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow,
            TvShowGenres =
            [
                new TvShowGenre
                {
                    TvShowId = Guid.NewGuid(),
                    GenreId = genre.Id,
                    Genre = genre
                }
            ]
        };
    }

    private static Episode CreateEpisode(int episodeNumber, string name) =>
        new()
        {
            Id = Guid.NewGuid(),
            SeasonId = Guid.NewGuid(),
            EpisodeNumber = episodeNumber,
            Name = name,
            VoteAverage = 8.0m,
            VoteCount = 100,
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow
        };
}
