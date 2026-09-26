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
    public void MatchesSearchFallbackAcceptsTurkishDiacriticAndAsciiVariants()
    {
        var matches = TitleYearMatcher.MatchesSearchFallback(
            "Organize Isler: Sazan Sarmali",
            null,
            "Organize İşler: Sazan Sarmalı",
            2019,
            new DateOnly(2019, 1, 1));

        Assert.True(matches);
    }

    [Fact]
    public void MatchesSearchFallbackAllowsMissingReleaseDateWhenYearProvided()
    {
        var matches = TitleYearMatcher.MatchesSearchFallback(
            "Arrival",
            null,
            "Arrival",
            2016,
            null);

        Assert.True(matches);
    }

    [Fact]
    public void MatchesSearchFallbackAnyAcceptsLocalizedAliasTitle()
    {
        var matches = TitleYearMatcher.MatchesSearchFallbackAny(
            "İçerde",
            2016,
            new DateOnly(2016, 9, 19),
            ["Insider", "The Voice of Romania", "İçerde"]);

        Assert.True(matches);
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
