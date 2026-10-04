using MovieApp.Application.Models.Providers;
using MovieApp.Application.Models.Search;
using MovieApp.Application.Services.Search;
using MovieApp.Infrastructure.Providers.Tmdb;

namespace MovieApp.UnitTests.Search;

public sealed class PopularDiscoverQualityTests
{
    [Fact]
    public void PopularDiscoverQualityUsesBenchmarkFloors()
    {
        Assert.Equal(100, PopularDiscoverQuality.MinimumVoteCountMovie);
        Assert.Equal(50, PopularDiscoverQuality.MinimumVoteCountTv);
    }

    [Fact]
    public void BuildMovieQueryPopularUsesPopularitySortAndMovieVoteFloorWithoutRatingGate()
    {
        var query = TmdbDiscoverQueryBuilder.BuildMovieQuery(CreatePopularCriteria());

        Assert.Contains("sort_by=popularity.desc", query);
        Assert.Contains($"vote_count.gte={PopularDiscoverQuality.MinimumVoteCountMovie}", query);
        Assert.DoesNotContain("vote_average.gte", query);
        Assert.Contains("include_adult=false", query);
    }

    [Fact]
    public void BuildTvQueryPopularUsesPopularitySortAndTvVoteFloorWithoutRatingGate()
    {
        var query = TmdbDiscoverQueryBuilder.BuildTvQuery(CreatePopularCriteria());

        Assert.Contains("sort_by=popularity.desc", query);
        Assert.Contains($"vote_count.gte={PopularDiscoverQuality.MinimumVoteCountTv}", query);
        Assert.DoesNotContain("vote_average.gte", query);
    }

    private static DiscoverProviderCriteria CreatePopularCriteria() =>
        new(
            DiscoverBrowseMode.Popular,
            1,
            [],
            null,
            null,
            null,
            null,
            null,
            null,
            null,
            null,
            null,
            [],
            [],
            DiscoverBrowseSort.PopularityDesc);
}
