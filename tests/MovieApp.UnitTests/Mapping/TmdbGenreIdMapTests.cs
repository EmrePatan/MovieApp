using MovieApp.Application.Mapping;

namespace MovieApp.UnitTests.Mapping;

public sealed class TmdbGenreIdMapTests
{
    [Theory]
    [InlineData("Action", 28)]
    [InlineData("Science Fiction", 878)]
    [InlineData("Drama", 18)]
    public void TryGetMovieGenreIdMapsKnownNames(string genreName, int expectedId)
    {
        var mapped = TmdbGenreIdMap.TryGetMovieGenreId(genreName, out var tmdbId);

        Assert.True(mapped);
        Assert.Equal(expectedId, tmdbId);
    }

    [Theory]
    [InlineData("Science Fiction", 10765)]
    [InlineData("Drama", 18)]
    public void TryGetTvGenreIdMapsKnownNames(string genreName, int expectedId)
    {
        var mapped = TmdbGenreIdMap.TryGetTvGenreId(genreName, out var tmdbId);

        Assert.True(mapped);
        Assert.Equal(expectedId, tmdbId);
    }

    [Fact]
    public void MapGenreNamesToMovieIdsIgnoresUnknownNames()
    {
        var mapped = TmdbGenreIdMap.MapGenreNamesToMovieIds(["Action", "Not A Real Genre"]);

        Assert.Single(mapped);
        Assert.Equal(28, mapped[0]);
    }
}
