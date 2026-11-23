using MovieApp.Contracts.Insights;

namespace MovieApp.Api.Mapping;

public static class InsightsCompareMapper
{
    private const int MaxSharedGenres = 4;
    private const int MaxTopGenres = 3;

    /// <summary>
    /// Match is the overlap of the two genre-share distributions (sum of the smaller share per
    /// genre, 0-100), so identical tastes score 100 and disjoint tastes score 0.
    /// </summary>
    public static InsightsCompareResponse Build(InsightsV3Response mine, InsightsV3Response theirs)
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

        return new InsightsCompareResponse(
            (int)Math.Clamp(Math.Round(overlap, MidpointRounding.AwayFromZero), 0, 100),
            shared,
            mine.MovieDna.TopGenres.Take(MaxTopGenres).Select(genre => genre.Name).ToList(),
            theirs.MovieDna.TopGenres.Take(MaxTopGenres).Select(genre => genre.Name).ToList(),
            mine.TimeInStories.TotalMinutes,
            theirs.TimeInStories.TotalMinutes);
    }
}
