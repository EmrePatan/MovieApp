using MovieApp.Application.Services.AiRecommendations;

namespace MovieApp.UnitTests.AiRecommendations;

public sealed class TitleYearMatcherTests
{
    [Fact]
    public void MatchesSearchFallbackAcceptsOriginalTitleMatch()
    {
        var matches = TitleYearMatcher.MatchesSearchFallback(
            "The Spanish Apartment",
            "L'Auberge Espagnole",
            "L'Auberge Espagnole",
            2002,
            new DateOnly(2002, 5, 17));

        Assert.True(matches);
    }

    [Fact]
    public void MatchesSearchFallbackAcceptsPunctuationVariant()
    {
        var matches = TitleYearMatcher.MatchesSearchFallback(
            "Spider-Man",
            null,
            "Spider Man",
            2002,
            new DateOnly(2002, 5, 3));

        Assert.True(matches);
    }

    [Fact]
    public void MatchesSearchFallbackAllowsYearWithinOne()
    {
        var matches = TitleYearMatcher.MatchesSearchFallback(
            "Blade Runner 2049",
            null,
            "Blade Runner 2049",
            2018,
            new DateOnly(2017, 10, 6));

        Assert.True(matches);
    }

    [Fact]
    public void MatchesSearchFallbackRejectsYearOutsideOne()
    {
        var matches = TitleYearMatcher.MatchesSearchFallback(
            "Arrival",
            null,
            "Arrival",
            2020,
            new DateOnly(2016, 11, 11));

        Assert.False(matches);
    }

    [Fact]
    public void MatchesKeepsStrictYearForHintValidation()
    {
        var matches = TitleYearMatcher.Matches(
            "Blade Runner 2049",
            null,
            "Blade Runner 2049",
            2018,
            new DateOnly(2017, 10, 6));

        Assert.False(matches);
    }

    [Fact]
    public void MatchesKeepsStrictTitleForHintValidation()
    {
        var matches = TitleYearMatcher.Matches(
            "Spider-Man",
            null,
            "Spider Man",
            2002,
            new DateOnly(2002, 5, 3));

        Assert.False(matches);
    }
}
