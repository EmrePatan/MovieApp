using MovieApp.Application.Models.Images;
using MovieApp.Application.Services.Images;

namespace MovieApp.UnitTests.Images;

public sealed class ImageGalleryOrdererTests
{
    [Fact]
    public void ToImagesResultPrefersRequestedLanguageThenNeutralThenEnglish()
    {
        var provider = new ProviderImagesResult(
            Backdrops:
            [
                new("/fr.jpg", "fr", 1.778m, 1920, 1080, 9.0m, 10),
                new("/en.jpg", "en", 1.778m, 1920, 1080, 8.0m, 20),
                new("/neutral.jpg", null, 1.778m, 1920, 1080, 7.0m, 30),
                new("/de.jpg", "de", 1.778m, 1920, 1080, 10.0m, 40)
            ],
            Posters: [],
            Logos: [],
            Profiles: []);

        var result = ImageGalleryOrderer.ToImagesResult(provider, "en");

        Assert.Equal("/en.jpg", result.Backdrops[0].FilePath);
        Assert.Equal("/neutral.jpg", result.Backdrops[1].FilePath);
        Assert.Equal("/de.jpg", result.Backdrops[2].FilePath);
        Assert.Equal("/fr.jpg", result.Backdrops[3].FilePath);
    }

    [Fact]
    public void ToImagesResultOrdersByVoteAverageAndVoteCountWithinLanguageRank()
    {
        var provider = new ProviderImagesResult(
            Backdrops:
            [
                new("/en-low.jpg", "en", 1.778m, 1920, 1080, 5.0m, 100),
                new("/en-high.jpg", "en", 1.778m, 1920, 1080, 8.0m, 5),
                new("/en-tie.jpg", "en", 1.778m, 1920, 1080, 8.0m, 20)
            ],
            Posters: [],
            Logos: [],
            Profiles: []);

        var result = ImageGalleryOrderer.ToImagesResult(provider, "en");

        Assert.Equal("/en-tie.jpg", result.Backdrops[0].FilePath);
        Assert.Equal("/en-high.jpg", result.Backdrops[1].FilePath);
        Assert.Equal("/en-low.jpg", result.Backdrops[2].FilePath);
    }

    [Fact]
    public void ResolveLanguagePrefersQueryOverAcceptLanguageHeader()
    {
        var resolved = ImageGalleryServiceHelper.ResolveLanguage("fr", "en-US,en;q=0.9");

        Assert.Equal("fr", resolved);
    }

    [Fact]
    public void ResolveLanguageParsesAcceptLanguageHeader()
    {
        var resolved = ImageGalleryServiceHelper.ResolveLanguage(null, "en-US,en;q=0.9");

        Assert.Equal("en-us", resolved);
    }
}
