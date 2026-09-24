using MovieApp.Application.Models.ExternalRatings;

namespace MovieApp.Application.Services.ExternalRatings;

public static class ExternalRatingSourceNormalizer
{
    private static readonly Dictionary<string, (string Canonical, int Scale)> SourceMap =
        new(StringComparer.OrdinalIgnoreCase)
        {
            ["imdb"] = ("imdb", 10),
            ["letterboxd"] = ("letterboxd", 5),
            ["tomatoes"] = ("tomatometer", 100),
            ["tomatoesaudience"] = ("popcornmeter", 100),
            ["popcorn"] = ("popcornmeter", 100),
            ["metacritic"] = ("metacritic", 100),
        };

    public static ExternalRatingItem? TryNormalize(string rawSource, decimal value, long? votes)
    {
        if (!SourceMap.TryGetValue(rawSource.Trim(), out var mapping))
        {
            return null;
        }

        if (!IsValidValue(value, mapping.Scale))
        {
            return null;
        }

        return new ExternalRatingItem(mapping.Canonical, value, mapping.Scale, votes);
    }

    private static bool IsValidValue(decimal value, int scale) =>
        value >= 0 && value <= scale;
}
