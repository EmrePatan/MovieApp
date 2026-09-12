using MovieApp.Infrastructure.Providers.Tmdb.TmdbMapping;
using MovieApp.Infrastructure.Providers.Tmdb.TmdbModels;

namespace MovieApp.UnitTests.Providers.Tmdb;

public sealed class TmdbMovieMapperTests
{
    [Fact]
    public void ToSummaryMapsSearchResultFields()
    {
        var result = TmdbMovieMapper.ToSummary(new TmdbMovieSearchResultJson
        {
            Id = 157336,
            Title = "Interstellar",
            Overview = "A team of explorers travel through a wormhole in space.",
            ReleaseDate = "2014-11-07",
            PosterPath = "/poster.jpg",
            VoteAverage = 8.7m,
            VoteCount = 25000
        });

        Assert.Equal("tmdb-157336", result.ExternalId);
        Assert.Equal(157336, result.TmdbId);
        Assert.Equal("Interstellar", result.Title);
        Assert.Equal(new DateOnly(2014, 11, 7), result.ReleaseDate);
        Assert.Equal("/poster.jpg", result.PosterPath);
    }

    [Fact]
    public void ToDetailsMapsMovieDetailsAndExternalIds()
    {
        var details = TmdbMovieMapper.ToDetails(new TmdbMovieDetailsResponseJson
        {
            Id = 157336,
            Title = "Interstellar",
            OriginalTitle = "Interstellar",
            Overview = "Overview",
            ReleaseDate = "2014-11-07",
            Runtime = 169,
            PosterPath = "poster.jpg",
            BackdropPath = "/backdrop.jpg",
            OriginalLanguage = "en",
            VoteAverage = 8.7m,
            VoteCount = 25000,
            Genres =
            [
                new TmdbGenreJson { Id = 12, Name = "Adventure" },
                new TmdbGenreJson { Id = 18, Name = "Drama" }
            ],
            ExternalIds = new TmdbExternalIdsJson
            {
                ImdbId = "tt0816692",
                TvdbId = 244021
            }
        });

        Assert.Equal("tt0816692", details.ImdbId);
        Assert.Equal(244021, details.TvdbId);
        Assert.Equal(169, details.RuntimeMinutes);
        Assert.Equal("/poster.jpg", details.PosterPath);
        Assert.Equal(["Adventure", "Drama"], details.Genres);
    }

    [Fact]
    public void ToDetailsHandlesMissingOptionalFields()
    {
        var details = TmdbMovieMapper.ToDetails(new TmdbMovieDetailsResponseJson
        {
            Id = 1,
            Title = "Untitled",
            VoteAverage = 0,
            VoteCount = 0
        });

        Assert.Null(details.Overview);
        Assert.Null(details.ReleaseDate);
        Assert.Null(details.PosterPath);
        Assert.Null(details.ImdbId);
        Assert.Empty(details.Genres);
    }

    [Fact]
    public void ToDetailsFallsBackToTopLevelImdbIdWhenExternalIdsMissing()
    {
        var details = TmdbMovieMapper.ToDetails(new TmdbMovieDetailsResponseJson
        {
            Id = 157336,
            Title = "Interstellar",
            ImdbId = "tt0816692"
        });

        Assert.Equal("tt0816692", details.ImdbId);
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    public void ToDetailsNormalizesEmptyOrWhitespaceImdbIdToNull(string imdbId)
    {
        var details = TmdbMovieMapper.ToDetails(new TmdbMovieDetailsResponseJson
        {
            Id = 348369,
            Title = "Avatar Days",
            ExternalIds = new TmdbExternalIdsJson
            {
                ImdbId = imdbId
            }
        });

        Assert.Null(details.ImdbId);
    }

    [Fact]
    public void ToDetailsNormalizesTopLevelEmptyImdbIdToNull()
    {
        var details = TmdbMovieMapper.ToDetails(new TmdbMovieDetailsResponseJson
        {
            Id = 348369,
            Title = "Avatar Days",
            ImdbId = string.Empty
        });

        Assert.Null(details.ImdbId);
    }

    [Fact]
    public void ToSearchResultMapsPaginationMetadata()
    {
        var result = TmdbMovieMapper.ToSearchResult(
            new TmdbMovieSearchResponseJson
            {
                Page = 2,
                TotalPages = 5,
                TotalResults = 100,
                Results =
                [
                    new TmdbMovieSearchResultJson { Id = 1, Title = "Movie 1" }
                ]
            },
            requestedPage: 2);

        Assert.Single(result.Results);
        Assert.Equal(2, result.Page);
        Assert.Equal(20, result.PageSize);
        Assert.Equal(100, result.TotalCount);
        Assert.Equal(5, result.TotalPages);
    }

    [Fact]
    public void ToSearchResultHandlesMissingPaginationFieldsSafely()
    {
        var result = TmdbMovieMapper.ToSearchResult(
            new TmdbMovieSearchResponseJson(),
            requestedPage: 3);

        Assert.Empty(result.Results);
        Assert.Equal(3, result.Page);
        Assert.Equal(0, result.TotalCount);
        Assert.Equal(0, result.TotalPages);
    }

    [Fact]
    public void ParseReleaseDateReturnsNullForInvalidValue()
    {
        Assert.Null(TmdbMovieMapper.ParseReleaseDate("invalid-date"));
    }
}
