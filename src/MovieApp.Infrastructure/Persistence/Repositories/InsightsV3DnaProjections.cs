using MovieApp.Application.Models.Insights;

namespace MovieApp.Infrastructure.Persistence.Repositories;

internal static class InsightsV3DnaProjections
{
    internal sealed record MovieWatchRow(
        DateTime WatchedAtUtc,
        int? RuntimeMinutes,
        int? ReleaseYear,
        IReadOnlyList<InsightsDnaGenreData> Genres)
    {
        internal InsightsDnaTitleData ToDnaTitle() => new(ReleaseYear, Genres);
    }

    internal sealed record EpisodeWatchRow(
        DateTime WatchedAtUtc,
        int? RuntimeMinutes,
        Guid TvShowId,
        int? FirstAirYear,
        IReadOnlyList<InsightsDnaGenreData> Genres)
    {
        internal InsightsDnaTitleData ToDnaTitle() => new(FirstAirYear, Genres);
    }

    internal static IReadOnlyList<InsightsDnaTitleData> ToAllTimeMovieTitles(
        IReadOnlyList<MovieWatchRow> rows) =>
        rows.Select(row => row.ToDnaTitle()).ToList();

    internal static IReadOnlyList<InsightsDnaTitleData> ToAllTimeTvShowTitles(
        IReadOnlyList<EpisodeWatchRow> rows) =>
        rows
            .GroupBy(row => row.TvShowId)
            .Select(group => group.First().ToDnaTitle())
            .ToList();

    internal static (
        IReadOnlyList<InsightsDnaTitleData> CurrentYear,
        IReadOnlyList<InsightsDnaTitleData> PreviousYear) SplitMovieTitlesByYear(
        IReadOnlyList<MovieWatchRow> rows,
        DateTime previousYearStart,
        DateTime previousYearEnd,
        DateTime currentYearStart,
        DateTime currentYearEnd) =>
        (
            rows
                .Where(row => row.WatchedAtUtc >= currentYearStart && row.WatchedAtUtc < currentYearEnd)
                .Select(row => row.ToDnaTitle())
                .ToList(),
            rows
                .Where(row => row.WatchedAtUtc >= previousYearStart && row.WatchedAtUtc < previousYearEnd)
                .Select(row => row.ToDnaTitle())
                .ToList());

    internal static (
        IReadOnlyList<InsightsDnaTitleData> CurrentYear,
        IReadOnlyList<InsightsDnaTitleData> PreviousYear) SplitTvShowTitlesByYear(
        IReadOnlyList<EpisodeWatchRow> rows,
        DateTime previousYearStart,
        DateTime previousYearEnd,
        DateTime currentYearStart,
        DateTime currentYearEnd) =>
        (
            rows
                .Where(row => row.WatchedAtUtc >= currentYearStart && row.WatchedAtUtc < currentYearEnd)
                .Select(row => row.ToDnaTitle())
                .ToList(),
            rows
                .Where(row => row.WatchedAtUtc >= previousYearStart && row.WatchedAtUtc < previousYearEnd)
                .Select(row => row.ToDnaTitle())
                .ToList());

    internal static IReadOnlyList<(DateTime WatchedAtUtc, int? RuntimeMinutes)> ToYearMovieWatches(
        IReadOnlyList<MovieWatchRow> rows,
        DateTime currentYearStart,
        DateTime currentYearEnd) =>
        rows
            .Where(row => row.WatchedAtUtc >= currentYearStart && row.WatchedAtUtc < currentYearEnd)
            .Select(row => (row.WatchedAtUtc, row.RuntimeMinutes))
            .ToList();

    internal static IReadOnlyList<(DateTime WatchedAtUtc, int? RuntimeMinutes)> ToYearEpisodeWatches(
        IReadOnlyList<EpisodeWatchRow> rows,
        DateTime currentYearStart,
        DateTime currentYearEnd) =>
        rows
            .Where(row => row.WatchedAtUtc >= currentYearStart && row.WatchedAtUtc < currentYearEnd)
            .Select(row => (row.WatchedAtUtc, row.RuntimeMinutes))
            .ToList();
}
