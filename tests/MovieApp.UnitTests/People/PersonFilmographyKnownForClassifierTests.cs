using MovieApp.Application.Models.People;
using MovieApp.Application.Models.Providers;
using MovieApp.Application.Services.People;

namespace MovieApp.UnitTests.People;

public sealed class PersonFilmographyKnownForClassifierTests
{
    [Theory]
    [InlineData("movie", new[] { 18 }, PersonFilmographyKnownForCategories.Movie)]
    [InlineData("movie", new[] { 10770 }, PersonFilmographyKnownForCategories.TelevisionMovie)]
    [InlineData("movie", new[] { 16 }, PersonFilmographyKnownForCategories.Animation)]
    [InlineData("movie", new[] { 99 }, PersonFilmographyKnownForCategories.Documentary)]
    [InlineData("tv", new[] { 18 }, PersonFilmographyKnownForCategories.ScriptedTelevision)]
    [InlineData("tv", new[] { 10767 }, PersonFilmographyKnownForCategories.TalkVarietyReality)]
    [InlineData("tv", new[] { 10764 }, PersonFilmographyKnownForCategories.TalkVarietyReality)]
    [InlineData("tv", new[] { 99 }, PersonFilmographyKnownForCategories.Documentary)]
    [InlineData("tv", new[] { 16 }, PersonFilmographyKnownForCategories.Animation)]
    [InlineData("tv", new[] { 10766 }, PersonFilmographyKnownForCategories.OtherTelevision)]
    public void ClassifiesCreditsByMediaTypeGenresAndTvType(
        string mediaType,
        int[] genreIds,
        string expectedCategory)
    {
        var credit = new PersonFilmographyCredit(
            mediaType,
            1,
            "Title",
            null,
            "Role",
            null,
            10m,
            7m,
            genreIds);

        var category = PersonFilmographyKnownForClassifier.Classify(credit);

        Assert.Equal(expectedCategory, category);
    }

    [Fact]
    public void ClassifiesMiniSeriesFromTvShowType()
    {
        var credit = new PersonFilmographyCredit(
            "tv",
            1,
            "Limited Series",
            null,
            "Lead",
            null,
            10m,
            8m,
            [18],
            "Miniseries");

        var category = PersonFilmographyKnownForClassifier.Classify(credit);

        Assert.Equal(PersonFilmographyKnownForCategories.MiniSeries, category);
    }
}
