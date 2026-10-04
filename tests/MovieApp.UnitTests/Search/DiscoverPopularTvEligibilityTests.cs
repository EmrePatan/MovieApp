using MovieApp.Application.Models.Providers;
using MovieApp.Application.Models.Search;
using MovieApp.Application.Services.Discovery;
using MovieApp.Application.Services.Search;

namespace MovieApp.UnitTests.Search;

public sealed class DiscoverPopularTvEligibilityTests
{
    [Fact]
    public void FilterEligible_Popular_ExcludesTalkGenreOnly()
    {
        var talk = CreateTvSummary([OnTvThisWeekContentSelector.TalkTmdbGenreId], "Late Night");
        var news = CreateTvSummary([OnTvThisWeekContentSelector.NewsTmdbGenreId], "Nightly News");
        var reality = CreateTvSummary([OnTvThisWeekContentSelector.RealityTmdbGenreId], "Competition");
        var drama = CreateTvSummary([18], "Scripted Drama");

        var filtered = DiscoverPopularTvEligibility.FilterEligible(
            DiscoverBrowseMode.Popular,
            [talk, news, reality, drama]);

        Assert.Equal(
            ["Nightly News", "Competition", "Scripted Drama"],
            filtered.Select(summary => summary.Title).ToList());
    }

    [Fact]
    public void FilterEligible_NonPopular_ReturnsAllSummaries()
    {
        var talk = CreateTvSummary([OnTvThisWeekContentSelector.TalkTmdbGenreId], "Talk");

        var filtered = DiscoverPopularTvEligibility.FilterEligible(
            DiscoverBrowseMode.Trending,
            [talk]);

        Assert.Single(filtered);
    }

    [Fact]
    public void IsEligibleForPopular_AllowsMissingGenreMetadata()
    {
        var unknownGenres = CreateTvSummary(null, "Unknown");

        Assert.True(DiscoverPopularTvEligibility.IsEligibleForPopular(DiscoverBrowseMode.Popular, unknownGenres));
    }

    private static TvShowProviderSummary CreateTvSummary(IReadOnlyList<int>? genreIds, string title) =>
        new(
            $"ext-{title}",
            1,
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
            50m,
            null,
            genreIds);
}
