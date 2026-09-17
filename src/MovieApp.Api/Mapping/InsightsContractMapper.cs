using MovieApp.Application.Models.Insights;
using MovieApp.Contracts.Insights;

namespace MovieApp.Api.Mapping;

public static class InsightsContractMapper
{
    public static InsightsSummaryResponse ToInsightsSummaryResponse(InsightsSummaryResult result) =>
        new(
            result.MemberSince,
            result.MovieDna.Select(ToInsightsMovieDnaLabelResponse).ToList(),
            new InsightsSummaryStatsResponse(
                result.Summary.MoviesWatched,
                result.Summary.EpisodesWatched,
                result.Summary.ShowsStarted,
                result.Summary.RatingsCount,
                result.Summary.AverageStarRating),
            new InsightsWatchingMixResponse(
                result.WatchingMix.MovieTitleCount,
                result.WatchingMix.SeriesTitleCount),
            result.GeneratedAtUtc);

    private static InsightsMovieDnaLabelResponse ToInsightsMovieDnaLabelResponse(
        InsightsMovieDnaLabelResult result) =>
        new(result.Code, result.Category, result.Label);
}
