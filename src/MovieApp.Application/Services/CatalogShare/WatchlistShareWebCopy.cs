using System.Globalization;

namespace MovieApp.Application.Services.CatalogShare;

internal static class WatchlistShareWebCopy
{
    internal sealed record Copy(
        string GenericTitle,
        string NotSharedTitle,
        string NotSharedBody,
        string ItemCountOne,
        string ItemCountMany,
        string OgDescriptionMany,
        string MovieLabel,
        string TvLabel);

    private static readonly Dictionary<string, Copy> ByLanguage = new(StringComparer.OrdinalIgnoreCase)
    {
        ["en"] = new(
            "Shared Watchlist",
            "Watchlist not available",
            "This Watchlist is no longer shared.",
            "1 title",
            "{0} titles",
            "{0} movies and TV shows shared on Movie Cave",
            "Movie",
            "TV"),
        ["tr"] = new(
            "Paylaşılan Watchlist",
            "Watchlist kullanılamıyor",
            "Bu watchlist artık paylaşılmıyor.",
            "1 içerik",
            "{0} içerik",
            "Movie Cave'de paylaşılan {0} film ve dizi",
            "Film",
            "Dizi"),
        ["de"] = new(
            "Geteilte Watchlist",
            "Watchlist nicht verfügbar",
            "Diese Watchlist wird nicht mehr geteilt.",
            "1 Titel",
            "{0} Titel",
            "{0} Filme und Serien auf Movie Cave geteilt",
            "Film",
            "Serie"),
        ["es"] = new(
            "Watchlist compartida",
            "Watchlist no disponible",
            "Esta watchlist ya no se comparte.",
            "1 título",
            "{0} títulos",
            "{0} películas y series compartidas en Movie Cave",
            "Película",
            "Serie"),
        ["fr"] = new(
            "Watchlist partagée",
            "Watchlist indisponible",
            "Cette watchlist n'est plus partagée.",
            "1 titre",
            "{0} titres",
            "{0} films et séries partagés sur Movie Cave",
            "Film",
            "Série"),
        ["it"] = new(
            "Watchlist condivisa",
            "Watchlist non disponibile",
            "Questa watchlist non è più condivisa.",
            "1 titolo",
            "{0} titoli",
            "{0} film e serie condivisi su Movie Cave",
            "Film",
            "Serie TV"),
        ["pt"] = new(
            "Watchlist compartilhada",
            "Watchlist indisponível",
            "Esta watchlist não é mais compartilhada.",
            "1 título",
            "{0} títulos",
            "{0} filmes e séries compartilhados no Movie Cave",
            "Filme",
            "Série"),
    };

    internal static Copy Resolve(string? acceptLanguageHeader)
    {
        var language = CatalogShareWebCopy.ResolveLanguageCode(acceptLanguageHeader);
        return ByLanguage.TryGetValue(language, out var copy) ? copy : ByLanguage["en"];
    }

    internal static string FormatOwnerTitle(string? displayName, Copy copy) =>
        string.IsNullOrWhiteSpace(displayName)
            ? copy.GenericTitle
            : $"{displayName}'s Watchlist";

    internal static string FormatItemCount(int count, Copy copy) =>
        count == 1 ? copy.ItemCountOne : string.Format(CultureInfo.InvariantCulture, copy.ItemCountMany, count);
}
