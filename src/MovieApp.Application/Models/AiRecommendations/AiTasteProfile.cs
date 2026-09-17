namespace MovieApp.Application.Models.AiRecommendations;

public sealed record AiTasteProfile(
    IReadOnlyList<AiTasteGenreAffinity> TopGenres,
    IReadOnlyList<AiTasteGenreAffinity> AvoidedGenres,
    IReadOnlyList<AiTasteMovieSignal> HighRatings,
    IReadOnlyList<AiTasteMovieSignal> LowRatings,
    IReadOnlyList<AiTasteMovieSignal> Favorites,
    IReadOnlyList<AiTasteMovieSignal> WatchlistHints,
    IReadOnlyList<AiTasteGenreAffinity> WeakTvGenres,
    IReadOnlyList<string> TasteKeywords,
    bool IsColdStart);

public sealed record AiTasteGenreAffinity(string Genre, double Weight);

public sealed record AiTasteMovieSignal(string Title, int? Year, int? Rating);
