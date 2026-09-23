using MovieApp.Application.Services.Localization;
using MovieApp.Application.Services.Notifications;
using MovieApp.Domain.Enums;

namespace MovieApp.UnitTests.Notifications;

public sealed class ReleaseNotificationBodyLocalizationTests
{
    [Theory]
    [InlineData("Now available", 1)]
    [InlineData("3 releases", 3)]
    [InlineData("1 new episode", 1)]
    [InlineData("4 new episodes", 4)]
    public void ParseEventCount_ReadsLeadingNumber(string body, int expected)
    {
        Assert.Equal(expected, ReleaseNotificationBodyLocalization.ParseEventCount(body));
    }

    [Fact]
    public void LocalizeBody_ReturnsSpanish_ForMovieReleased()
    {
        var body = ReleaseNotificationBodyLocalization.LocalizeBody(
            UserReleaseNotificationType.MovieReleased,
            "Now available",
            ContentLocaleResolver.SpanishSpain);

        Assert.Equal("Ya disponible", body);
    }

    [Fact]
    public void LocalizeBody_ReturnsTurkish_ForMovieReleased()
    {
        var body = ReleaseNotificationBodyLocalization.LocalizeBody(
            UserReleaseNotificationType.MovieReleased,
            "Now available",
            ContentLocaleResolver.TurkishTurkey);

        Assert.Equal("Şimdi yayında", body);
    }

    [Fact]
    public void LocalizeBody_ReturnsSpanish_ForMultipleReleases()
    {
        var body = ReleaseNotificationBodyLocalization.LocalizeBody(
            UserReleaseNotificationType.MovieReleased,
            "2 releases",
            ContentLocaleResolver.SpanishSpain);

        Assert.Equal("2 estrenos", body);
    }

    [Theory]
    [InlineData(ContentLocaleResolver.GermanGermany, "Jetzt verfügbar")]
    [InlineData(ContentLocaleResolver.FrenchFrance, "Disponible maintenant")]
    [InlineData(ContentLocaleResolver.ItalianItaly, "Ora disponibile")]
    [InlineData(ContentLocaleResolver.PortugueseBrazil, "Disponível agora")]
    public void LocalizeBody_ReturnsLocalizedMovieReleased(string locale, string expected)
    {
        var body = ReleaseNotificationBodyLocalization.LocalizeBody(
            UserReleaseNotificationType.MovieReleased,
            "Now available",
            locale);

        Assert.Equal(expected, body);
    }
}
