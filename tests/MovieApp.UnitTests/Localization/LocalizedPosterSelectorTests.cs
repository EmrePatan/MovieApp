using MovieApp.Application.Models.Images;
using MovieApp.Application.Services.Localization;

namespace MovieApp.UnitTests.Localization;

public sealed class LocalizedPosterSelectorTests
{
    private const string CanonicalEnglishPoster = "/canonical-en.jpg";
    private const string TurkishPoster = "/tr-alev-alev.jpg";
    private const string NeutralPoster = "/neutral.jpg";
    private const string FrenchPoster = "/fr.jpg";
    private const string TurkishFanPoster = "/tr-fan.jpg";

    [Fact]
    public void SelectPrefersTurkishPosterForTurkishOriginal()
    {
        var posters = CreateAlevAlevPosters();

        var selection = LocalizedPosterSelector.Select(posters, "tr", CanonicalEnglishPoster, originalLanguage: "tr");

        Assert.True(selection.ShouldPersist);
        Assert.Equal(TurkishPoster, selection.PosterPath);
    }

    [Fact]
    public void SelectKeepsCanonicalPosterForImportedTitleWithTurkishArtwork()
    {
        var posters = new List<ProviderImageResult>
        {
            CreatePoster(CanonicalEnglishPoster, "en", 8m, 100),
            CreatePoster(TurkishFanPoster, "tr", 9m, 200),
        };

        var selection = LocalizedPosterSelector.Select(posters, "tr", CanonicalEnglishPoster, originalLanguage: "en");

        Assert.False(selection.ShouldPersist);
        Assert.Equal(CanonicalEnglishPoster, selection.PosterPath);
    }

    [Fact]
    public void ShouldUseStoredLocalizedPoster_ReturnsFalseForImportedTitleWithDifferentArtwork()
    {
        var useLocalized = LocalizedPosterSelector.ShouldUseStoredLocalizedPoster(
            CanonicalEnglishPoster,
            TurkishFanPoster,
            ContentLocaleResolver.TurkishTurkey,
            originalLanguage: "en");

        Assert.False(useLocalized);
    }

    [Fact]
    public void SelectPrefersEnglishPosterWhenEnglishLanguageKeyRequested()
    {
        var posters = CreateAlevAlevPosters();

        var selection = LocalizedPosterSelector.Select(posters, "en", CanonicalEnglishPoster);

        Assert.True(selection.ShouldPersist);
        Assert.Equal(CanonicalEnglishPoster, selection.PosterPath);
    }

    [Fact]
    public void SelectFallsBackToNeutralWhenTurkishMissing()
    {
        var posters = new List<ProviderImageResult>
        {
            CreatePoster(CanonicalEnglishPoster, "en", 8m, 100),
            CreatePoster(NeutralPoster, null, 7m, 50),
        };

        var selection = LocalizedPosterSelector.Select(posters, "tr", CanonicalEnglishPoster, originalLanguage: "tr");

        Assert.True(selection.ShouldPersist);
        Assert.Equal(NeutralPoster, selection.PosterPath);
    }

    [Fact]
    public void SelectFallsBackToCanonicalWhenNoTurkishOrNeutral()
    {
        var posters = new List<ProviderImageResult>
        {
            CreatePoster(FrenchPoster, "fr", 9m, 200),
            CreatePoster(CanonicalEnglishPoster, "en", 8m, 100),
        };

        var selection = LocalizedPosterSelector.Select(posters, "tr", CanonicalEnglishPoster, originalLanguage: "en");

        Assert.False(selection.ShouldPersist);
        Assert.Equal(CanonicalEnglishPoster, selection.PosterPath);
    }

    [Fact]
    public void SelectOrdersByVotesWithinLanguageTier()
    {
        var posters = new List<ProviderImageResult>
        {
            CreatePoster("/tr-low.jpg", "tr", 5m, 100),
            CreatePoster(TurkishPoster, "tr", 8m, 20),
        };

        var selection = LocalizedPosterSelector.Select(posters, "tr", CanonicalEnglishPoster, originalLanguage: "tr");

        Assert.Equal(TurkishPoster, selection.PosterPath);
    }

    [Fact]
    public void SelectPrefersCanonicalPosterPathWithinLanguageTier()
    {
        const string canonicalTurkishPoster = "/tr-main.jpg";
        var posters = new List<ProviderImageResult>
        {
            CreatePoster("/tr-scene.jpg", "tr", 9m, 500, width: 500, aspectRatio: 0.667m),
            CreatePoster(canonicalTurkishPoster, "tr", 6m, 10, width: 500, aspectRatio: 0.667m),
        };

        var selection = LocalizedPosterSelector.Select(
            posters,
            "tr",
            canonicalTurkishPoster,
            originalLanguage: "tr",
            primaryOriginCountryCode: "TR");

        Assert.Equal(canonicalTurkishPoster, selection.PosterPath);
    }

    private static List<ProviderImageResult> CreateAlevAlevPosters() =>
    [
        CreatePoster(CanonicalEnglishPoster, "en", 8m, 100, width: 500, aspectRatio: 0.667m),
        CreatePoster(TurkishPoster, "tr", 7.5m, 40, width: 500, aspectRatio: 0.667m),
        CreatePoster(FrenchPoster, "fr", 9m, 200, width: 500, aspectRatio: 0.667m),
    ];

    private static ProviderImageResult CreatePoster(
        string filePath,
        string? language,
        decimal voteAverage,
        int voteCount,
        int width = 500,
        decimal aspectRatio = 0.667m) =>
        new(filePath, language, aspectRatio, width, 750, voteAverage, voteCount);
}
