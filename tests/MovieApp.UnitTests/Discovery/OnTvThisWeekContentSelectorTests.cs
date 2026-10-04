using MovieApp.Application.Models.Discovery;
using MovieApp.Application.Models.Providers;
using MovieApp.Application.Services.Discovery;

namespace MovieApp.UnitTests.Discovery;

public sealed class OnTvThisWeekContentSelectorTests
{
    [Fact]
    public void HomeRailExcludesTalkNewsAndRealityInSourceOrder()
    {
        var summaries = new[]
        {
            Summary(1, "Talk", [OnTvThisWeekContentSelector.TalkTmdbGenreId]),
            Summary(2, "Drama", [18]),
            Summary(3, "News", [OnTvThisWeekContentSelector.NewsTmdbGenreId]),
            Summary(4, "Doc", [99]),
            Summary(5, "Reality", [OnTvThisWeekContentSelector.RealityTmdbGenreId]),
            Summary(6, "No Genres", []),
        };

        var ordered = OnTvThisWeekContentSelector.OrderForPresentation(
            summaries,
            OnTvThisWeekPresentationIntent.HomeRail);

        Assert.Equal([2, 4, 6], ordered.Select(item => item.TmdbId).ToArray());
    }

    [Fact]
    public void HomeRailKeepsAnimationAndScriptedTitles()
    {
        var summaries = new[]
        {
            Summary(1, "Anime", [16]),
            Summary(2, "Crime", [80]),
        };

        var ordered = OnTvThisWeekContentSelector.OrderForPresentation(
            summaries,
            OnTvThisWeekPresentationIntent.HomeRail);

        Assert.Equal([1, 2], ordered.Select(item => item.TmdbId).ToArray());
    }

    [Fact]
    public void DiscoverBrowseOrderForPresentationExcludesTalkNewsAndRealityInSourceOrder()
    {
        var summaries = new[]
        {
            Summary(1, "Talk", [OnTvThisWeekContentSelector.TalkTmdbGenreId]),
            Summary(2, "Drama", [18]),
            Summary(3, "News", [OnTvThisWeekContentSelector.NewsTmdbGenreId]),
            Summary(4, "Doc", [99]),
            Summary(5, "Reality", [OnTvThisWeekContentSelector.RealityTmdbGenreId]),
            Summary(6, "No Genres", []),
        };

        var ordered = OnTvThisWeekContentSelector.OrderForPresentation(
            summaries,
            OnTvThisWeekPresentationIntent.DiscoverBrowse);

        Assert.Equal([2, 4, 6], ordered.Select(item => item.TmdbId).ToArray());
    }

    private static TvShowProviderSummary Summary(int tmdbId, string title, int[] genreIds) =>
        new(
            $"fake-{tmdbId}",
            tmdbId,
            null,
            null,
            title,
            title,
            "Overview",
            new DateOnly(2024, 1, 1),
            "/poster.jpg",
            null,
            "en",
            8m,
            100,
            GenreTmdbIds: genreIds.Length == 0 ? null : genreIds);
}
