using System.Text.RegularExpressions;
using MovieApp.Application.Services.Localization;
using MovieApp.Domain.Enums;

namespace MovieApp.Application.Services.Notifications;

public static class ReleaseNotificationBodyLocalization
{
    private static readonly Regex LeadingCountPattern = new(@"^(\d+)\b", RegexOptions.CultureInvariant);

    public static string LocalizeBody(
        UserReleaseNotificationType notificationType,
        string? storedBody,
        string contentLocale)
    {
        return LocalizeBody(notificationType, ParseEventCount(storedBody), contentLocale);
    }

    public static string LocalizeBody(
        UserReleaseNotificationType notificationType,
        int eventCount,
        string contentLocale)
    {
        var normalizedLocale = ContentLocaleResolver.Normalize(contentLocale);

        return normalizedLocale switch
        {
            ContentLocaleResolver.TurkishTurkey => LocalizeTurkish(notificationType, eventCount),
            ContentLocaleResolver.SpanishSpain => LocalizeSpanish(notificationType, eventCount),
            _ => LocalizeEnglish(notificationType, eventCount),
        };
    }

    internal static int ParseEventCount(string? storedBody)
    {
        if (string.IsNullOrWhiteSpace(storedBody))
        {
            return 1;
        }

        var match = LeadingCountPattern.Match(storedBody.Trim());
        if (match.Success && int.TryParse(match.Groups[1].Value, out var parsed) && parsed > 0)
        {
            return parsed;
        }

        return 1;
    }

    private static string LocalizeEnglish(UserReleaseNotificationType notificationType, int eventCount) =>
        notificationType switch
        {
            UserReleaseNotificationType.NewEpisodes =>
                eventCount == 1 ? "1 new episode" : $"{eventCount} new episodes",
            UserReleaseNotificationType.NewSeason =>
                eventCount == 1 ? "New season premiere" : $"{eventCount} new season premieres",
            UserReleaseNotificationType.MovieReleased =>
                eventCount == 1 ? "Now available" : $"{eventCount} releases",
            _ => string.Empty,
        };

    private static string LocalizeTurkish(UserReleaseNotificationType notificationType, int eventCount) =>
        notificationType switch
        {
            UserReleaseNotificationType.NewEpisodes =>
                eventCount == 1 ? "1 yeni bölüm" : $"{eventCount} yeni bölüm",
            UserReleaseNotificationType.NewSeason =>
                eventCount == 1 ? "Yeni sezon yayında" : $"{eventCount} yeni sezon",
            UserReleaseNotificationType.MovieReleased =>
                eventCount == 1 ? "Şimdi yayında" : $"{eventCount} yeni çıkış",
            _ => string.Empty,
        };

    private static string LocalizeSpanish(UserReleaseNotificationType notificationType, int eventCount) =>
        notificationType switch
        {
            UserReleaseNotificationType.NewEpisodes =>
                eventCount == 1 ? "1 episodio nuevo" : $"{eventCount} episodios nuevos",
            UserReleaseNotificationType.NewSeason =>
                eventCount == 1 ? "Estreno de nueva temporada" : $"{eventCount} estrenos de temporada",
            UserReleaseNotificationType.MovieReleased =>
                eventCount == 1 ? "Ya disponible" : $"{eventCount} estrenos",
            _ => string.Empty,
        };
}
