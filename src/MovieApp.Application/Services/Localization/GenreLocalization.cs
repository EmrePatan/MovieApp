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

    private static readonly Dictionary<string, string> GermanNames =
        new(StringComparer.OrdinalIgnoreCase)
        {
            ["Action"] = "Action",
            ["Adventure"] = "Abenteuer",
            ["Action & Adventure"] = "Action & Abenteuer",
            ["Animation"] = "Animation",
            ["Comedy"] = "Komödie",
            ["Crime"] = "Krimi",
            ["Documentary"] = "Dokumentarfilm",
            ["Drama"] = "Drama",
            ["Family"] = "Familie",
            ["Fantasy"] = "Fantasy",
            ["History"] = "Historie",
            ["Horror"] = "Horror",
            ["Kids"] = "Kinder",
            ["Music"] = "Musik",
            ["Mystery"] = "Mystery",
            ["News"] = "Nachrichten",
            ["Reality"] = "Reality",
            ["Romance"] = "Liebesfilm",
            ["Science Fiction"] = "Science-Fiction",
            ["Sci-Fi & Fantasy"] = "Sci-Fi & Fantasy",
            ["Soap"] = "Seifenoper",
            ["Talk"] = "Talkshow",
            ["TV Movie"] = "TV-Film",
            ["Thriller"] = "Thriller",
            ["War"] = "Kriegsfilm",
            ["War & Politics"] = "Krieg & Politik",
            ["Western"] = "Western",
        };

    private static readonly Dictionary<string, string> FrenchNames =
        new(StringComparer.OrdinalIgnoreCase)
        {
            ["Action"] = "Action",
            ["Adventure"] = "Aventure",
            ["Action & Adventure"] = "Action & Aventure",
            ["Animation"] = "Animation",
            ["Comedy"] = "Comédie",
            ["Crime"] = "Crime",
            ["Documentary"] = "Documentaire",
            ["Drama"] = "Drame",
            ["Family"] = "Familial",
            ["Fantasy"] = "Fantastique",
            ["History"] = "Histoire",
            ["Horror"] = "Horreur",
            ["Kids"] = "Jeunesse",
            ["Music"] = "Musique",
            ["Mystery"] = "Mystère",
            ["News"] = "Actualités",
            ["Reality"] = "Télé-réalité",
            ["Romance"] = "Romance",
            ["Science Fiction"] = "Science-fiction",
            ["Sci-Fi & Fantasy"] = "Science-fiction & Fantastique",
            ["Soap"] = "Feuilleton",
            ["Talk"] = "Talk-show",
            ["TV Movie"] = "Téléfilm",
            ["Thriller"] = "Thriller",
            ["War"] = "Guerre",
            ["War & Politics"] = "Guerre & Politique",
            ["Western"] = "Western",
        };

    private static readonly Dictionary<string, string> ItalianNames =
        new(StringComparer.OrdinalIgnoreCase)
        {
            ["Action"] = "Azione",
            ["Adventure"] = "Avventura",
            ["Action & Adventure"] = "Azione e avventura",
            ["Animation"] = "Animazione",
            ["Comedy"] = "Commedia",
            ["Crime"] = "Crime",
            ["Documentary"] = "Documentario",
            ["Drama"] = "Dramma",
            ["Family"] = "Famiglia",
            ["Fantasy"] = "Fantasy",
            ["History"] = "Storia",
            ["Horror"] = "Horror",
            ["Kids"] = "Bambini",
            ["Music"] = "Musica",
            ["Mystery"] = "Mistero",
            ["News"] = "Notizie",
            ["Reality"] = "Reality",
            ["Romance"] = "Romantico",
            ["Science Fiction"] = "Fantascienza",
            ["Sci-Fi & Fantasy"] = "Fantascienza e fantasy",
            ["Soap"] = "Soap opera",
            ["Talk"] = "Talk show",
            ["TV Movie"] = "Film TV",
            ["Thriller"] = "Thriller",
            ["War"] = "Guerra",
            ["War & Politics"] = "Guerre e politica",
            ["Western"] = "Western",
        };

    private static readonly Dictionary<string, string> PortugueseBrazilNames =
        new(StringComparer.OrdinalIgnoreCase)
        {
            ["Action"] = "Ação",
            ["Adventure"] = "Aventura",
            ["Action & Adventure"] = "Ação e aventura",
            ["Animation"] = "Animação",
            ["Comedy"] = "Comédia",
            ["Crime"] = "Crime",
            ["Documentary"] = "Documentário",
            ["Drama"] = "Drama",
            ["Family"] = "Família",
            ["Fantasy"] = "Fantasia",
            ["History"] = "História",
            ["Horror"] = "Terror",
            ["Kids"] = "Infantil",
            ["Music"] = "Música",
            ["Mystery"] = "Mistério",
            ["News"] = "Notícias",
            ["Reality"] = "Reality",
            ["Romance"] = "Romance",
            ["Science Fiction"] = "Ficção científica",
            ["Sci-Fi & Fantasy"] = "Ficção científica e fantasia",
            ["Soap"] = "Novela",
            ["Talk"] = "Talk show",
            ["TV Movie"] = "Filme para TV",
            ["Thriller"] = "Suspense",
            ["War"] = "Guerra",
            ["War & Politics"] = "Guerra e política",
            ["Western"] = "Faroeste",
        };

    private static readonly Dictionary<string, Dictionary<string, string>> LocalizedNamesByLocale =
        new(StringComparer.OrdinalIgnoreCase)
        {
            [ContentLocaleResolver.TurkishTurkey] = TurkishNames,
            [ContentLocaleResolver.SpanishSpain] = SpanishNames,
            [ContentLocaleResolver.GermanGermany] = GermanNames,
            [ContentLocaleResolver.FrenchFrance] = FrenchNames,
            [ContentLocaleResolver.ItalianItaly] = ItalianNames,
            [ContentLocaleResolver.PortugueseBrazil] = PortugueseBrazilNames,
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
