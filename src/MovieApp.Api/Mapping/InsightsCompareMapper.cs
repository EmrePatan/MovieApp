using MovieApp.Contracts.Insights;

namespace MovieApp.Api.Mapping;

public static class InsightsCompareMapper
{
    private const int MaxSharedGenres = 4;
    private const int MaxTopGenres = 3;

    private const decimal GenreWeight = 0.7m;

    /// <summary>
    /// Genre overlap is the sum of the smaller share per genre (0-100): identical tastes score 100,
    /// disjoint tastes 0. When both sides have enough keyword data, 30% of the score comes from
    /// keyword/theme similarity instead.
    /// </summary>
    public static InsightsCompareResponse Build(
        InsightsV3Response mine,
        InsightsV3Response theirs,
        int? keywordMatchPercent = null,
        IReadOnlyList<InsightsCompareKeywordResponse>? sharedKeywords = null)
    {
        var myShares = mine.YourTaste.Genres.ToDictionary(genre => genre.GenreId);
        var theirShares = theirs.YourTaste.Genres.ToDictionary(genre => genre.GenreId);

        var overlap = myShares
            .Where(pair => theirShares.ContainsKey(pair.Key))
            .Sum(pair => Math.Min(pair.Value.SharePercent, theirShares[pair.Key].SharePercent));

        var shared = myShares
            .Where(pair => theirShares.ContainsKey(pair.Key))
            .OrderByDescending(pair => Math.Min(pair.Value.SharePercent, theirShares[pair.Key].SharePercent))
            .Take(MaxSharedGenres)
            .Select(pair => new InsightsCompareGenreResponse(
                pair.Key,
                pair.Value.Name,
                pair.Value.SharePercent,
                theirShares[pair.Key].SharePercent))
            .ToList();

        var genrePercent = Math.Clamp(Math.Round(overlap, MidpointRounding.AwayFromZero), 0, 100);
        var matchPercent = keywordMatchPercent is { } keyword
            ? Math.Clamp(
                Math.Round(genrePercent * GenreWeight + keyword * (1 - GenreWeight), MidpointRounding.AwayFromZero),
                0,
                100)
            : genrePercent;

        return new InsightsCompareResponse(
            (int)matchPercent,
            shared,
            mine.MovieDna.TopGenres.Take(MaxTopGenres).Select(genre => genre.Name).ToList(),
            theirs.MovieDna.TopGenres.Take(MaxTopGenres).Select(genre => genre.Name).ToList(),
            mine.TimeInStories.TotalMinutes,
            theirs.TimeInStories.TotalMinutes,
            keywordMatchPercent,
            sharedKeywords ?? []);
    }
}
