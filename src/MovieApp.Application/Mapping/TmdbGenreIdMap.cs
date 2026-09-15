namespace MovieApp.Application.Mapping;

public static class TmdbGenreIdMap
{
    private static readonly Dictionary<string, int> MovieGenreIds =
        new(StringComparer.OrdinalIgnoreCase)
        {
            ["Action"] = 28,
            ["Adventure"] = 12,
            ["Animation"] = 16,
            ["Comedy"] = 35,
            ["Crime"] = 80,
            ["Documentary"] = 99,
            ["Drama"] = 18,
            ["Family"] = 10751,
            ["Fantasy"] = 14,
            ["History"] = 36,
            ["Horror"] = 27,
            ["Music"] = 10402,
            ["Mystery"] = 9648,
            ["Romance"] = 10749,
            ["Science Fiction"] = 878,
            ["TV Movie"] = 10770,
            ["Thriller"] = 53,
            ["War"] = 10752,
            ["Western"] = 37
        };

    private static readonly Dictionary<string, int> TvGenreIds =
        new(StringComparer.OrdinalIgnoreCase)
        {
            ["Action"] = 10759,
            ["Action & Adventure"] = 10759,
            ["Adventure"] = 10759,
            ["Animation"] = 16,
            ["Comedy"] = 35,
            ["Crime"] = 80,
            ["Documentary"] = 99,
            ["Drama"] = 18,
            ["Family"] = 10751,
            ["Fantasy"] = 10765,
            ["History"] = 36,
            ["Horror"] = 27,
            ["Kids"] = 10762,
            ["Music"] = 10402,
            ["Mystery"] = 9648,
            ["News"] = 10763,
            ["Reality"] = 10764,
            ["Romance"] = 10749,
            ["Science Fiction"] = 10765,
            ["Sci-Fi & Fantasy"] = 10765,
            ["Soap"] = 10766,
            ["Talk"] = 10767,
            ["Thriller"] = 53,
            ["War"] = 10768,
            ["War & Politics"] = 10768,
            ["Western"] = 37
        };

    public static bool TryGetMovieGenreId(string genreName, out int tmdbGenreId) =>
        MovieGenreIds.TryGetValue(genreName, out tmdbGenreId);

    public static bool TryGetTvGenreId(string genreName, out int tmdbGenreId) =>
        TvGenreIds.TryGetValue(genreName, out tmdbGenreId);

    public static IReadOnlyList<int> MapGenreNamesToMovieIds(IEnumerable<string> genreNames) =>
        MapGenreNames(genreNames, TryGetMovieGenreId);

    public static IReadOnlyList<int> MapGenreNamesToTvIds(IEnumerable<string> genreNames) =>
        MapGenreNames(genreNames, TryGetTvGenreId);

    private static List<int> MapGenreNames(
        IEnumerable<string> genreNames,
        TryGetGenreId tryGetGenreId)
    {
        var mapped = new HashSet<int>();

        foreach (var genreName in genreNames)
        {
            if (tryGetGenreId(genreName, out var tmdbGenreId))
            {
                mapped.Add(tmdbGenreId);
            }
        }

        return mapped.OrderBy(id => id).ToList();
    }

    private delegate bool TryGetGenreId(string genreName, out int tmdbGenreId);
}
