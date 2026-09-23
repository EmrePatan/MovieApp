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
            ContentLocaleResolver.GermanGermany => LocalizeGerman(notificationType, eventCount),
            ContentLocaleResolver.FrenchFrance => LocalizeFrench(notificationType, eventCount),
            ContentLocaleResolver.ItalianItaly => LocalizeItalian(notificationType, eventCount),
            ContentLocaleResolver.PortugueseBrazil => LocalizePortugueseBrazil(notificationType, eventCount),
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

    private static string LocalizeGerman(UserReleaseNotificationType notificationType, int eventCount) =>
        notificationType switch
        {
            UserReleaseNotificationType.NewEpisodes =>
                eventCount == 1 ? "1 neue Folge" : $"{eventCount} neue Folgen",
            UserReleaseNotificationType.NewSeason =>
                eventCount == 1 ? "Neue Staffel verfügbar" : $"{eventCount} neue Staffeln",
            UserReleaseNotificationType.MovieReleased =>
                eventCount == 1 ? "Jetzt verfügbar" : $"{eventCount} neue Veröffentlichungen",
            _ => string.Empty,
        };

    private static string LocalizeFrench(UserReleaseNotificationType notificationType, int eventCount) =>
        notificationType switch
        {
            UserReleaseNotificationType.NewEpisodes =>
                eventCount == 1 ? "1 nouvel épisode" : $"{eventCount} nouveaux épisodes",
            UserReleaseNotificationType.NewSeason =>
                eventCount == 1 ? "Nouvelle saison disponible" : $"{eventCount} nouvelles saisons",
            UserReleaseNotificationType.MovieReleased =>
                eventCount == 1 ? "Disponible maintenant" : $"{eventCount} nouvelles sorties",
            _ => string.Empty,
        };

    private static string LocalizeItalian(UserReleaseNotificationType notificationType, int eventCount) =>
        notificationType switch
        {
            UserReleaseNotificationType.NewEpisodes =>
                eventCount == 1 ? "1 nuovo episodio" : $"{eventCount} nuovi episodi",
            UserReleaseNotificationType.NewSeason =>
                eventCount == 1 ? "Nuova stagione disponibile" : $"{eventCount} nuove stagioni",
            UserReleaseNotificationType.MovieReleased =>
                eventCount == 1 ? "Ora disponibile" : $"{eventCount} nuove uscite",
            _ => string.Empty,
        };

    private static string LocalizePortugueseBrazil(UserReleaseNotificationType notificationType, int eventCount) =>
        notificationType switch
        {
            UserReleaseNotificationType.NewEpisodes =>
                eventCount == 1 ? "1 novo episódio" : $"{eventCount} novos episódios",
            UserReleaseNotificationType.NewSeason =>
                eventCount == 1 ? "Nova temporada disponível" : $"{eventCount} novas temporadas",
            UserReleaseNotificationType.MovieReleased =>
                eventCount == 1 ? "Disponível agora" : $"{eventCount} novos lançamentos",
            _ => string.Empty,
        };
}
