using MovieApp.Application.Services.Localization;

namespace MovieApp.UnitTests.AiRecommendations;

public sealed class DeterministicAiRecommendationReasonLocalizationTests
{
    [Fact]
    public void TurkishGenreAffinityReason_IsLocalizedWithoutEnglishPrefix()
    {
        var localized = RecommendationReasonLocalization.Localize(
            "Because you liked Science Fiction",
            ContentLocaleResolver.TurkishTurkey);

        Assert.Equal("Bilim Kurgu türünü sevdiğin için", localized);
        Assert.DoesNotContain("Because you liked", localized, StringComparison.OrdinalIgnoreCase);
    }

    [Theory]
    [InlineData("tr")]
    [InlineData("tr-TR")]
    public void TurkishLocaleVariants_UseSameLocalizedGenreReason(string locale)
    {
        var localized = RecommendationReasonLocalization.Localize(
            "Because you liked Science Fiction",
            locale);

        Assert.Equal("Bilim Kurgu türünü sevdiğin için", localized);
    }

    [Fact]
    public void EnglishLocale_PreservesExistingEnglishReason()
    {
        var localized = RecommendationReasonLocalization.Localize(
            "Because you liked Science Fiction",
            ContentLocaleResolver.EnglishUnitedStates);

        Assert.Equal("Because you liked Science Fiction", localized);
    }

    [Fact]
    public void UnsupportedLocale_FallsBackToEnglishReason()
    {
        var localized = RecommendationReasonLocalization.Localize(
            "Because you liked Science Fiction",
            "ja-JP");

        Assert.Equal("Because you liked Science Fiction", localized);
    }

    [Fact]
    public void ColdStartPopularReason_IsLocalizedForTurkish()
    {
        var localized = RecommendationReasonLocalization.Localize(
            "Popular right now",
            ContentLocaleResolver.TurkishTurkey);

        Assert.Equal("Şu anda popüler", localized);
    }
}
