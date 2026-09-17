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

        if (eligibleTitles.Count < MinimumEligibleTitles)
        {
            return new InsightsTasteResult([]);
        }

        var weights = new Dictionary<Guid, (string Name, decimal Weight)>();

        foreach (var title in eligibleTitles)
        {
            var contribution = 1m / title.Genres.Count;
            foreach (var genre in title.Genres)
            {
                if (weights.TryGetValue(genre.GenreId, out var existing))
                {
                    weights[genre.GenreId] = (existing.Name, existing.Weight + contribution);
                    continue;
                }

                weights[genre.GenreId] = (genre.Name, contribution);
            }
        }

        var denominator = weights.Values.Sum(item => item.Weight);
        if (denominator <= 0)
        {
            return new InsightsTasteResult([]);
        }

        var genres = weights
            .Select(pair => new InsightsTasteGenreResult(
                pair.Key,
                pair.Value.Name,
                pair.Value.Weight,
                Math.Round(pair.Value.Weight * 100m / denominator, 1, MidpointRounding.AwayFromZero)))
            .OrderByDescending(genre => genre.Weight)
            .ThenBy(genre => genre.Name, StringComparer.Ordinal)
            .Take(TopGenreCount)
            .ToList();

        return new InsightsTasteResult(genres);
    }
}
