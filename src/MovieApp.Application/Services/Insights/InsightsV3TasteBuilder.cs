using MovieApp.Application.Models.Insights;

namespace MovieApp.Application.Services.Insights;

public static class InsightsV3TasteBuilder
{
    public const int MinimumTitlesPerYear = 3;
    public const decimal MinimumShareDeltaPercent = 5m;

    public static InsightsV3TasteSectionResult Build(InsightsV3RawData raw)
    {
        var genres = InsightsTasteBuilder.BuildGenres(
            raw.AllTimeGenreContributions,
            raw.AllTimeTitlesWithGenres);
        var risingGenre = TryBuildRisingGenre(
            raw.CurrentYearGenreContributions,
            raw.CurrentYearTitlesWithGenres,
            raw.PreviousYearGenreContributions,
            raw.PreviousYearTitlesWithGenres);

        return new InsightsV3TasteSectionResult(genres, risingGenre);
    }

    public static InsightsV3RisingGenreResult? TryBuildRisingGenre(
        IReadOnlyList<InsightsDnaTitleData> currentYearMovieTitles,
        IReadOnlyList<InsightsDnaTitleData> currentYearTvShowTitles,
        IReadOnlyList<InsightsDnaTitleData> previousYearMovieTitles,
        IReadOnlyList<InsightsDnaTitleData> previousYearTvShowTitles)
    {
        var currentTitles = currentYearMovieTitles.Concat(currentYearTvShowTitles)
            .Where(title => title.Genres.Count > 0)
            .ToList();
        var previousTitles = previousYearMovieTitles.Concat(previousYearTvShowTitles)
            .Where(title => title.Genres.Count > 0)
            .ToList();

        return TryBuildRisingGenre(
            InsightsGenreContributions.FromTitles(currentTitles),
            currentTitles.Count,
            InsightsGenreContributions.FromTitles(previousTitles),
            previousTitles.Count);
    }

    public static InsightsV3RisingGenreResult? TryBuildRisingGenre(
        IReadOnlyList<InsightsV3GenreContribution> currentContributions,
        int currentEligibleTitles,
        IReadOnlyList<InsightsV3GenreContribution> previousContributions,
        int previousEligibleTitles)
    {
        if (currentEligibleTitles < MinimumTitlesPerYear || previousEligibleTitles < MinimumTitlesPerYear)
        {
            return null;
        }

        var currentShares = ComputeGenreShares(currentContributions);
        var previousShares = ComputeGenreShares(previousContributions);

        var risingCandidate = currentShares
            .Select(pair =>
            {
                previousShares.TryGetValue(pair.Key, out var previousShare);
                return new
                {
                    pair.Key.GenreId,
                    pair.Key.Name,
                    CurrentShare = pair.Value,
                    PreviousShare = previousShare,
                    Delta = pair.Value - previousShare,
                };
            })
            .Where(candidate => candidate.Delta >= MinimumShareDeltaPercent)
            .OrderByDescending(candidate => candidate.Delta)
            .ThenByDescending(candidate => candidate.CurrentShare)
            .ThenBy(candidate => candidate.Name, StringComparer.Ordinal)
            .FirstOrDefault();

        if (risingCandidate is null)
        {
            return null;
        }

        return new InsightsV3RisingGenreResult(
            risingCandidate.GenreId,
            risingCandidate.Name,
            Math.Round(risingCandidate.CurrentShare, 1, MidpointRounding.AwayFromZero),
            Math.Round(risingCandidate.PreviousShare, 1, MidpointRounding.AwayFromZero),
            Math.Round(risingCandidate.Delta, 1, MidpointRounding.AwayFromZero));
    }

    private static Dictionary<(Guid GenreId, string Name), decimal> ComputeGenreShares(
        IReadOnlyList<InsightsV3GenreContribution> contributions)
    {
        var weights = InsightsGenreContributions.SumWeightsByGenreAndName(contributions);
        var denominator = weights.Values.Sum();
        if (denominator <= 0)
        {
            return [];
        }

        return weights.ToDictionary(
            pair => pair.Key,
            pair => pair.Value * 100m / denominator);
    }
}
