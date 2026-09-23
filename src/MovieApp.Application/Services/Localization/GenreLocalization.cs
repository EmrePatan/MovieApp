namespace MovieApp.Application.Services.Localization;

public static class GenreLocalization
{
    private static readonly Dictionary<string, string> TurkishNames =
        new(StringComparer.OrdinalIgnoreCase)
        {
            ["Action"] = "Aksiyon",
            ["Adventure"] = "Macera",
            ["Action & Adventure"] = "Aksiyon ve Macera",
            ["Animation"] = "Animasyon",
            ["Comedy"] = "Komedi",
            ["Crime"] = "Suç",
            ["Documentary"] = "Belgesel",
            ["Drama"] = "Dram",
            ["Family"] = "Aile",
            ["Fantasy"] = "Fantastik",
            ["History"] = "Tarih",
            ["Horror"] = "Korku",
            ["Kids"] = "Çocuk",
            ["Music"] = "Müzik",
            ["Mystery"] = "Gizem",
            ["News"] = "Haber",
            ["Reality"] = "Gerçeklik",
            ["Romance"] = "Romantik",
            ["Science Fiction"] = "Bilim Kurgu",
            ["Sci-Fi & Fantasy"] = "Bilim Kurgu ve Fantastik",
            ["Soap"] = "Pembe Dizi",
            ["Talk"] = "Söyleşi",
            ["TV Movie"] = "TV Filmi",
            ["Thriller"] = "Gerilim",
            ["War"] = "Savaş",
            ["War & Politics"] = "Savaş ve Politika",
            ["Western"] = "Kovboy",
        };

    private static readonly Dictionary<string, string> SpanishNames =
        new(StringComparer.OrdinalIgnoreCase)
        {
            ["Action"] = "Acción",
            ["Adventure"] = "Aventura",
            ["Action & Adventure"] = "Acción y aventura",
            ["Animation"] = "Animación",
            ["Comedy"] = "Comedia",
            ["Crime"] = "Crimen",
            ["Documentary"] = "Documental",
            ["Drama"] = "Drama",
            ["Family"] = "Familia",
            ["Fantasy"] = "Fantasía",
            ["History"] = "Historia",
            ["Horror"] = "Terror",
            ["Kids"] = "Infantil",
            ["Music"] = "Música",
            ["Mystery"] = "Misterio",
            ["News"] = "Noticias",
            ["Reality"] = "Reality",
            ["Romance"] = "Romance",
            ["Science Fiction"] = "Ciencia ficción",
            ["Sci-Fi & Fantasy"] = "Ciencia ficción y fantasía",
            ["Soap"] = "Telenovela",
            ["Talk"] = "Entrevistas",
            ["TV Movie"] = "Película para TV",
            ["Thriller"] = "Suspense",
            ["War"] = "Bélica",
            ["War & Politics"] = "Guerra y política",
            ["Western"] = "Western",
        };

    private static readonly Dictionary<string, Dictionary<string, string>> LocalizedNamesByLocale =
        new(StringComparer.OrdinalIgnoreCase)
        {
            [ContentLocaleResolver.TurkishTurkey] = TurkishNames,
            [ContentLocaleResolver.SpanishSpain] = SpanishNames,
        };

    public static string Localize(string canonicalName, string contentLocale)
    {
        if (string.IsNullOrWhiteSpace(canonicalName) ||
            !ContentLocaleResolver.RequiresLocalization(contentLocale))
        {
            return canonicalName;
        }

        var normalizedLocale = ContentLocaleResolver.Normalize(contentLocale);
        if (!LocalizedNamesByLocale.TryGetValue(normalizedLocale, out var localizedNames) ||
            !localizedNames.TryGetValue(canonicalName.Trim(), out var localizedName))
        {
            return canonicalName;
        }

        return localizedName;
    }
}
