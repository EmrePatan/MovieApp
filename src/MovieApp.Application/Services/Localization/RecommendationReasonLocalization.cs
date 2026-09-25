using System.Text.RegularExpressions;

namespace MovieApp.Application.Services.Localization;

public static class RecommendationReasonLocalization
{
    private static readonly Regex BecauseYouLikedPattern =
        new(@"^Because you liked (.+)$", RegexOptions.CultureInvariant | RegexOptions.IgnoreCase);

    private static readonly Regex BecauseYouRatedPattern =
        new(@"^Because you rated (.+) highly$", RegexOptions.CultureInvariant | RegexOptions.IgnoreCase);

    private static readonly Regex BecauseYouWatchedPattern =
        new(@"^Because you watched (.+)$", RegexOptions.CultureInvariant | RegexOptions.IgnoreCase);

    private static readonly Regex SimilarToPattern =
        new(@"^Similar to (.+)$", RegexOptions.CultureInvariant | RegexOptions.IgnoreCase);

    public static string Localize(string? englishReason, string contentLocale)
    {
        if (string.IsNullOrWhiteSpace(englishReason) ||
            !ContentLocaleResolver.RequiresLocalization(contentLocale))
        {
            return englishReason ?? string.Empty;
        }

        var normalizedLocale = ContentLocaleResolver.Normalize(contentLocale);
        var reason = englishReason.Trim();

        if (BecauseYouLikedPattern.IsMatch(reason))
        {
            var match = BecauseYouLikedPattern.Match(reason);
            return FormatBecauseYouLiked(match.Groups[1].Value, normalizedLocale);
        }

        if (BecauseYouRatedPattern.IsMatch(reason))
        {
            var match = BecauseYouRatedPattern.Match(reason);
            return FormatBecauseYouRated(match.Groups[1].Value, normalizedLocale);
        }

        if (BecauseYouWatchedPattern.IsMatch(reason))
        {
            var match = BecauseYouWatchedPattern.Match(reason);
            return FormatBecauseYouWatched(match.Groups[1].Value, normalizedLocale);
        }

        if (SimilarToPattern.IsMatch(reason))
        {
            var match = SimilarToPattern.Match(reason);
            return FormatSimilarTo(match.Groups[1].Value, normalizedLocale);
        }

        return reason switch
        {
            "From your watchlist" => LocalizeWatchlist(normalizedLocale),
            "Trending right now" => LocalizeTrendingNow(normalizedLocale),
            "Trending" => LocalizeTrending(normalizedLocale),
            "Popular right now" => LocalizePopularNow(normalizedLocale),
            "Popular in your favorite genres" => LocalizePopularGenres(normalizedLocale),
            "Top rated" => LocalizeTopRated(normalizedLocale),
            "Based on your favorites" => LocalizeBasedOnFavorites(normalizedLocale),
            _ => englishReason,
        };
    }

    private static string FormatBecauseYouLiked(string genre, string locale)
    {
        var displayGenre = GenreLocalization.Localize(genre.Trim(), locale);
        return locale switch
        {
            ContentLocaleResolver.TurkishTurkey => $"{displayGenre} türünü sevdiğin için",
            ContentLocaleResolver.SpanishSpain => $"Porque te gusta {displayGenre}",
            ContentLocaleResolver.GermanGermany => $"Weil dir {displayGenre} gefällt",
            ContentLocaleResolver.FrenchFrance => $"Parce que vous aimez {displayGenre}",
            ContentLocaleResolver.ItalianItaly => $"Perché ti piace {displayGenre}",
            ContentLocaleResolver.PortugueseBrazil => $"Porque você gosta de {displayGenre}",
            _ => $"Because you liked {displayGenre}",
        };
    }

    private static string FormatBecauseYouRated(string title, string locale) => locale switch
    {
        ContentLocaleResolver.TurkishTurkey => $"{title} için yüksek puan verdiğin için",
        ContentLocaleResolver.SpanishSpain => $"Porque valoraste muy bien {title}",
        ContentLocaleResolver.GermanGermany => $"Weil du {title} hoch bewertet hast",
        ContentLocaleResolver.FrenchFrance => $"Parce que vous avez bien noté {title}",
        ContentLocaleResolver.ItalianItaly => $"Perché hai valutato molto bene {title}",
        ContentLocaleResolver.PortugueseBrazil => $"Porque você avaliou muito bem {title}",
        _ => $"Because you rated {title} highly",
    };

    private static string FormatBecauseYouWatched(string title, string locale) => locale switch
    {
        ContentLocaleResolver.TurkishTurkey => $"{title} izlediğin için",
        ContentLocaleResolver.SpanishSpain => $"Porque viste {title}",
        ContentLocaleResolver.GermanGermany => $"Weil du {title} gesehen hast",
        ContentLocaleResolver.FrenchFrance => $"Parce que vous avez regardé {title}",
        ContentLocaleResolver.ItalianItaly => $"Perché hai guardato {title}",
        ContentLocaleResolver.PortugueseBrazil => $"Porque você assistiu {title}",
        _ => $"Because you watched {title}",
    };

    private static string FormatSimilarTo(string title, string locale) => locale switch
    {
        ContentLocaleResolver.TurkishTurkey => $"{title} ile benzer",
        ContentLocaleResolver.SpanishSpain => $"Similar a {title}",
        ContentLocaleResolver.GermanGermany => $"Ähnlich wie {title}",
        ContentLocaleResolver.FrenchFrance => $"Similaire à {title}",
        ContentLocaleResolver.ItalianItaly => $"Simile a {title}",
        ContentLocaleResolver.PortugueseBrazil => $"Semelhante a {title}",
        _ => $"Similar to {title}",
    };

    private static string LocalizeWatchlist(string locale) => locale switch
    {
        ContentLocaleResolver.TurkishTurkey => "İzleme listenizden",
        ContentLocaleResolver.SpanishSpain => "De tu lista de seguimiento",
        ContentLocaleResolver.GermanGermany => "Von deiner Merkliste",
        ContentLocaleResolver.FrenchFrance => "Depuis votre liste",
        ContentLocaleResolver.ItalianItaly => "Dalla tua watchlist",
        ContentLocaleResolver.PortugueseBrazil => "Da sua lista",
        _ => "From your watchlist",
    };

    private static string LocalizeTrendingNow(string locale) => locale switch
    {
        ContentLocaleResolver.TurkishTurkey => "Şu anda trend",
        ContentLocaleResolver.SpanishSpain => "Tendencia ahora",
        ContentLocaleResolver.GermanGermany => "Gerade im Trend",
        ContentLocaleResolver.FrenchFrance => "Tendance du moment",
        ContentLocaleResolver.ItalianItaly => "Di tendenza ora",
        ContentLocaleResolver.PortugueseBrazil => "Em alta agora",
        _ => "Trending right now",
    };

    private static string LocalizeTrending(string locale) => locale switch
    {
        ContentLocaleResolver.TurkishTurkey => "Trend",
        ContentLocaleResolver.SpanishSpain => "Tendencia",
        ContentLocaleResolver.GermanGermany => "Trend",
        ContentLocaleResolver.FrenchFrance => "Tendance",
        ContentLocaleResolver.ItalianItaly => "Di tendenza",
        ContentLocaleResolver.PortugueseBrazil => "Em alta",
        _ => "Trending",
    };

    private static string LocalizePopularNow(string locale) => locale switch
    {
        ContentLocaleResolver.TurkishTurkey => "Şu anda popüler",
        ContentLocaleResolver.SpanishSpain => "Popular ahora",
        ContentLocaleResolver.GermanGermany => "Gerade beliebt",
        ContentLocaleResolver.FrenchFrance => "Populaire en ce moment",
        ContentLocaleResolver.ItalianItaly => "Popolare ora",
        ContentLocaleResolver.PortugueseBrazil => "Popular agora",
        _ => "Popular right now",
    };

    private static string LocalizePopularGenres(string locale) => locale switch
    {
        ContentLocaleResolver.TurkishTurkey => "Favori türlerinde popüler",
        ContentLocaleResolver.SpanishSpain => "Popular en tus géneros favoritos",
        ContentLocaleResolver.GermanGermany => "Beliebt in deinen Lieblingsgenres",
        ContentLocaleResolver.FrenchFrance => "Populaire dans vos genres favoris",
        ContentLocaleResolver.ItalianItaly => "Popolare nei tuoi generi preferiti",
        ContentLocaleResolver.PortugueseBrazil => "Popular nos seus gêneros favoritos",
        _ => "Popular in your favorite genres",
    };

    private static string LocalizeBasedOnFavorites(string locale) => locale switch
    {
        ContentLocaleResolver.TurkishTurkey => "Favorilerine göre",
        ContentLocaleResolver.SpanishSpain => "Según tus favoritos",
        ContentLocaleResolver.GermanGermany => "Basierend auf deinen Favoriten",
        ContentLocaleResolver.FrenchFrance => "Selon vos favoris",
        ContentLocaleResolver.ItalianItaly => "In base ai tuoi preferiti",
        ContentLocaleResolver.PortugueseBrazil => "Com base nos seus favoritos",
        _ => "Based on your favorites",
    };

    private static string LocalizeTopRated(string locale) => locale switch
    {
        ContentLocaleResolver.TurkishTurkey => "En yüksek puanlı",
        ContentLocaleResolver.SpanishSpain => "Mejor valorados",
        ContentLocaleResolver.GermanGermany => "Top bewertet",
        ContentLocaleResolver.FrenchFrance => "Les mieux notés",
        ContentLocaleResolver.ItalianItaly => "I più votati",
        ContentLocaleResolver.PortugueseBrazil => "Mais bem avaliados",
        _ => "Top rated",
    };
}
