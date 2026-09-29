using MovieApp.Application.Models.Insights;

namespace MovieApp.Application.Services.Insights;

public static class InsightsTasteBuilder
{
    public const int MinimumEligibleTitles = 3;
    public const int TopGenreCount = 6;

    public static InsightsTasteResult Build(InsightsAnalyticsRawData raw)
    {
        var titles = raw.MovieTitles.Concat(raw.TvShowTitles).ToList();
        var eligibleTitles = titles
            .Where(title => title.Genres.Count > 0)
            .ToList();

        return new InsightsTasteResult(BuildGenres(
            InsightsGenreContributions.FromTitles(eligibleTitles),
            eligibleTitles.Count));
    }

    public static IReadOnlyList<InsightsTasteGenreResult> BuildGenres(
        IReadOnlyList<InsightsV3GenreContribution> contributions,
        int eligibleTitles)
    {
        if (eligibleTitles < MinimumEligibleTitles)
        {
            return [];
        }

        var weights = InsightsGenreContributions.SumWeightsByGenreId(contributions);
        var denominator = weights.Values.Sum(item => item.Weight);
        if (denominator <= 0)
        {
            return [];
        }

        return weights
            .Select(pair => new InsightsTasteGenreResult(
                pair.Key,
                pair.Value.Name,
                pair.Value.Weight,
                Math.Round(pair.Value.Weight * 100m / denominator, 1, MidpointRounding.AwayFromZero)))
            .OrderByDescending(genre => genre.Weight)
            .ThenBy(genre => genre.Name, StringComparer.Ordinal)
            .Take(TopGenreCount)
            .ToList();
    }
}
