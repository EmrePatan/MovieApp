using System.Globalization;

namespace MovieApp.Application.Services.CatalogShare;

internal static class CatalogShareWebCopy
{
    internal sealed record Copy(
        string ContentNotFoundTitle,
        string ContentNotFoundBody,
        string MovieLabel,
        string TvLabel,
        string OpenInApp,
        string ViewOnMovieCave);

    private static readonly Dictionary<string, Copy> ByLanguage = new(StringComparer.OrdinalIgnoreCase)
    {
        ["en"] = new(
            "Content not found",
            "This title is not available on Movie Cave.",
            "Movie",
            "TV series",
            "Open in Movie Cave",
            "View on Movie Cave"),
        ["tr"] = new(
            "İçerik bulunamadı",
            "Bu içerik Movie Cave'de bulunamadı.",
            "Film",
            "Dizi",
            "Movie Cave'de Aç",
            "Movie Cave'de incele"),
        ["de"] = new(
            "Inhalt nicht gefunden",
            "Dieser Titel ist auf Movie Cave nicht verfügbar.",
            "Film",
            "Serie",
            "In Movie Cave öffnen",
            "Auf Movie Cave ansehen"),
        ["es"] = new(
            "Contenido no encontrado",
            "Este título no está disponible en Movie Cave.",
            "Película",
            "Serie",
            "Abrir en Movie Cave",
            "Ver en Movie Cave"),
        ["fr"] = new(
            "Contenu introuvable",
            "Ce titre n'est pas disponible sur Movie Cave.",
            "Film",
            "Série",
            "Ouvrir dans Movie Cave",
            "Voir sur Movie Cave"),
        ["it"] = new(
            "Contenuto non trovato",
            "Questo titolo non è disponibile su Movie Cave.",
            "Film",
            "Serie TV",
            "Apri in Movie Cave",
            "Guarda su Movie Cave"),
        ["pt"] = new(
            "Conteúdo não encontrado",
            "Este título não está disponível no Movie Cave.",
            "Filme",
            "Série",
            "Abrir no Movie Cave",
            "Ver no Movie Cave"),
    };

    internal static Copy Resolve(string? acceptLanguageHeader)
    {
        var language = ResolveLanguageCode(acceptLanguageHeader);
        return ByLanguage.TryGetValue(language, out var copy) ? copy : ByLanguage["en"];
    }

    internal static string ResolveLanguageCode(string? acceptLanguageHeader)
    {
        if (string.IsNullOrWhiteSpace(acceptLanguageHeader))
        {
            return "en";
        }

        foreach (var segment in acceptLanguageHeader.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
        {
            var token = segment.Split(';', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)[0];
            if (token.Length < 2)
            {
                continue;
            }

            var normalized = token.Length >= 2 ? token[..2].ToLowerInvariant() : token.ToLowerInvariant();
            if (ByLanguage.ContainsKey(normalized))
            {
                return normalized;
            }
        }

        return "en";
    }

    internal static string HtmlLang(string languageCode) =>
        languageCode.Equals("pt", StringComparison.OrdinalIgnoreCase) ? "pt" : languageCode;

    internal static string FormatYear(int? year) =>
        year is null ? string.Empty : year.Value.ToString(CultureInfo.InvariantCulture);
}
