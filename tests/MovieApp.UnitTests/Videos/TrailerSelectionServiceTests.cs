using MovieApp.Application.Models.Videos;
using MovieApp.Application.Services.Videos;

namespace MovieApp.UnitTests.Videos;

public sealed class TrailerSelectionServiceTests
{
    [Fact]
    public void SelectPrimaryPrefersOfficialTrailerWithinTrailerCandidates()
    {
        var videos = new[]
        {
            CreateVideo("trailer-a", type: "Trailer", official: false, language: "en", publishedAt: Days(2)),
            CreateVideo("trailer-b", type: "Trailer", official: true, language: "en", publishedAt: Days(1)),
        };

        var primary = TrailerSelectionService.SelectPrimary(videos, "en");

        Assert.NotNull(primary);
        Assert.Equal("https://www.youtube.com/watch?v=trailer-b", primary.WatchUrl);
    }

    [Fact]
    public void SelectPrimaryPrefersUnofficialTrailerOverOfficialTeaser()
    {
        var videos = new[]
        {
            CreateVideo("teaser-official", type: "Teaser", official: true, language: "en", publishedAt: Days(2)),
            CreateVideo("trailer-unofficial", type: "Trailer", official: false, language: "en", publishedAt: Days(1)),
        };

        var primary = TrailerSelectionService.SelectPrimary(videos, "en");

        Assert.NotNull(primary);
        Assert.Equal("Trailer", primary.Type);
        Assert.Equal("https://www.youtube.com/watch?v=trailer-unofficial", primary.WatchUrl);
    }

    [Fact]
    public void SelectPrimaryFallsBackToTeaserWhenNoTrailerExists()
    {
        var videos = new[]
        {
            CreateVideo("teaser-only", type: "Teaser", official: true, language: "en", publishedAt: Days(1)),
        };

        var primary = TrailerSelectionService.SelectPrimary(videos, "en");

        Assert.NotNull(primary);
        Assert.Equal("Teaser", primary.Type);
    }

    [Fact]
    public void SelectPrimaryPrefersOriginalLanguageWithinEquivalentCandidates()
    {
        var videos = new[]
        {
            CreateVideo("trailer-en", type: "Trailer", official: true, language: "en", publishedAt: Days(2)),
            CreateVideo("trailer-tr", type: "Trailer", official: true, language: "tr", publishedAt: Days(1)),
        };

        var primary = TrailerSelectionService.SelectPrimary(videos, "tr");

        Assert.NotNull(primary);
        Assert.Equal("https://www.youtube.com/watch?v=trailer-tr", primary.WatchUrl);
    }

    [Fact]
    public void SelectPrimaryFallsBackToEnglishWhenOriginalLanguageMissing()
    {
        var videos = new[]
        {
            CreateVideo("trailer-fr", type: "Trailer", official: true, language: "fr", publishedAt: Days(2)),
            CreateVideo("trailer-en", type: "Trailer", official: true, language: "en", publishedAt: Days(1)),
        };

        var primary = TrailerSelectionService.SelectPrimary(videos, "tr");

        Assert.NotNull(primary);
        Assert.Equal("https://www.youtube.com/watch?v=trailer-en", primary.WatchUrl);
    }

    [Fact]
    public void SelectPrimaryFallsBackToAnyLanguageWhenNoPreferredLanguageExists()
    {
        var publishedAt = Days(1);
        var videos = new[]
        {
            CreateVideo("trailer-fr", type: "Trailer", official: true, language: "fr", publishedAt: publishedAt),
            CreateVideo("trailer-de", type: "Trailer", official: true, language: "de", publishedAt: publishedAt),
        };

        var primary = TrailerSelectionService.SelectPrimary(videos, "tr");

        Assert.NotNull(primary);
        Assert.Equal("https://www.youtube.com/watch?v=trailer-de", primary.WatchUrl);
    }

    [Fact]
    public void SelectPrimaryUsesNewestPublishedAtAsTieBreak()
    {
        var videos = new[]
        {
            CreateVideo("trailer-old", type: "Trailer", official: true, language: "en", publishedAt: Days(1)),
            CreateVideo("trailer-new", type: "Trailer", official: true, language: "en", publishedAt: Days(3)),
        };

        var primary = TrailerSelectionService.SelectPrimary(videos, "en");

        Assert.NotNull(primary);
        Assert.Equal("https://www.youtube.com/watch?v=trailer-new", primary.WatchUrl);
    }

    [Fact]
    public void SelectPrimaryUsesStableKeyTieBreak()
    {
        var publishedAt = Days(1);
        var videos = new[]
        {
            CreateVideo("trailer-z", type: "Trailer", official: true, language: "en", publishedAt: publishedAt),
            CreateVideo("trailer-a", type: "Trailer", official: true, language: "en", publishedAt: publishedAt),
        };

        var primary = TrailerSelectionService.SelectPrimary(videos, "en");

        Assert.NotNull(primary);
        Assert.Equal("https://www.youtube.com/watch?v=trailer-a", primary.WatchUrl);
    }

    [Fact]
    public void SelectPrimaryExcludesUnsupportedSite()
    {
        var videos = new[]
        {
            new ProviderVideoResult("Vimeo", "Trailer", "vimeo-key", "Vimeo Trailer", true, "en", "US", Days(1)),
        };

        var primary = TrailerSelectionService.SelectPrimary(videos, "en");

        Assert.Null(primary);
    }

    [Fact]
    public void SelectPrimaryExcludesInvalidKey()
    {
        var videos = new[]
        {
            CreateVideo("bad/key", type: "Trailer", official: true, language: "en", publishedAt: Days(1)),
        };

        var primary = TrailerSelectionService.SelectPrimary(videos, "en");

        Assert.Null(primary);
    }

    [Fact]
    public void SelectPrimaryReturnsNullForEmptyList()
    {
        var primary = TrailerSelectionService.SelectPrimary([], "en");

        Assert.Null(primary);
    }

    private static ProviderVideoResult CreateVideo(
        string key,
        string type,
        bool official,
        string language,
        DateTimeOffset publishedAt) =>
        new(
            Site: "YouTube",
            Type: type,
            Key: key,
            Name: "Sample",
            Official: official,
            Language: language,
            Country: "US",
            PublishedAt: publishedAt);

    private static DateTimeOffset Days(int day) =>
        new DateTimeOffset(2024, 1, day, 0, 0, 0, TimeSpan.Zero);
}
