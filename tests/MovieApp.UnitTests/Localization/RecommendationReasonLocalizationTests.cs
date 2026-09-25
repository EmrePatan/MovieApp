using MovieApp.Application.Services.Localization;

namespace MovieApp.UnitTests.Localization;

public sealed class RecommendationReasonLocalizationTests
{
    [Theory]
    [InlineData(ContentLocaleResolver.GermanGermany, "Von deiner Merkliste")]
    [InlineData(ContentLocaleResolver.FrenchFrance, "Depuis votre liste")]
    [InlineData(ContentLocaleResolver.ItalianItaly, "Dalla tua watchlist")]
    [InlineData(ContentLocaleResolver.PortugueseBrazil, "Da sua lista")]
    public void Localize_ReturnsLocalizedWatchlistReason(string locale, string expected)
    {
        var localized = RecommendationReasonLocalization.Localize("From your watchlist", locale);

        Assert.Equal(expected, localized);
    }

    [Fact]
    public void Localize_ReturnsGerman_ForBecauseYouLikedGenre()
    {
        var localized = RecommendationReasonLocalization.Localize(
            "Because you liked Action",
            ContentLocaleResolver.GermanGermany);

        Assert.Equal("Weil dir Action gefällt", localized);
    }

    [Fact]
    public void Localize_UsesLocalizedTurkishGenreName_ForBecauseYouLiked()
    {
        var localized = RecommendationReasonLocalization.Localize(
            "Because you liked Science Fiction",
            ContentLocaleResolver.TurkishTurkey);

        Assert.Equal("Bilim Kurgu türünü sevdiğin için", localized);
    }

    [Fact]
    public void Localize_ReturnsEnglish_ForEnglishLocale()
    {
        var localized = RecommendationReasonLocalization.Localize(
            "Trending right now",
            ContentLocaleResolver.EnglishUnitedStates);

        Assert.Equal("Trending right now", localized);
    }
}
