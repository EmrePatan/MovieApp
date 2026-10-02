using Microsoft.Extensions.Options;
using MovieApp.Application.Configuration;
using MovieApp.Application.Models.Providers;
using MovieApp.Application.Models.Search;
using MovieApp.Application.Services.Search;
using MovieApp.Infrastructure.Providers.Tmdb;

namespace MovieApp.UnitTests.Search;

public sealed class TmdbDiscoverQueryBuilderTests
{
    [Fact]
    public void BuildMovieQuery_NewestSortCapsFarFutureReleases()
    {
        var query = TmdbDiscoverQueryBuilder.BuildMovieQuery(CreateCriteria(
            mode: DiscoverBrowseMode.Trending,
            sort: DiscoverBrowseSort.ReleaseDesc));

        var cutoff = DiscoverCatalogSortPolicy.GetFutureReleaseCutoffDate(DateTime.UtcNow);
        Assert.Contains($"primary_release_date.lte={cutoff:yyyy-MM-dd}", query);
        Assert.Contains("sort_by=primary_release_date.desc", query);
        Assert.DoesNotContain("vote_count.gte", query);
    }

    [Fact]
    public void BuildMovieQuery_RatingDescAppliesMinimumVoteFloor()
    {
        var query = TmdbDiscoverQueryBuilder.BuildMovieQuery(CreateCriteria(
            mode: DiscoverBrowseMode.Trending,
            sort: DiscoverBrowseSort.RatingDesc));

        Assert.Contains(
            $"vote_count.gte={DiscoverCatalogSortPolicy.RatingRankedSortMinimumVoteCount}",
            query);
    }

    [Fact]
    public void BuildMovieQuery_TopRatedBrowseModeUsesCuratedVoteFloor()
    {
        var query = TmdbDiscoverQueryBuilder.BuildMovieQuery(CreateCriteria(
            mode: DiscoverBrowseMode.TopRated,
            sort: DiscoverBrowseSort.RatingDesc));

        Assert.Contains(
            $"vote_count.gte={DiscoverCatalogSortPolicy.CuratedTopRatedListMinimumVoteCount}",
            query);
    }

    [Fact]
    public void BuildMovieQuery_RatingDescPreservesStricterUserVoteCount()
    {
        var query = TmdbDiscoverQueryBuilder.BuildMovieQuery(CreateCriteria(
            mode: DiscoverBrowseMode.Trending,
            sort: DiscoverBrowseSort.RatingDesc,
            minVoteCount: 500));

        Assert.Contains("vote_count.gte=500", query);
    }

    [Fact]
    public void BuildMovieQuery_PopularModeAppliesVoteFloor()
    {
        var query = TmdbDiscoverQueryBuilder.BuildMovieQuery(CreateCriteria(
            mode: DiscoverBrowseMode.Popular,
            sort: DiscoverBrowseSort.PopularityDesc));

        Assert.Contains($"vote_count.gte={PopularDiscoverQuality.MinimumVoteCountMovie}", query);
    }

    [Fact]
    public void BuildTvQuery_PopularModeAppliesLowerVoteFloor()
    {
        var query = TmdbDiscoverQueryBuilder.BuildTvQuery(CreateCriteria(
            mode: DiscoverBrowseMode.Popular,
            sort: DiscoverBrowseSort.PopularityDesc));

        Assert.Contains($"vote_count.gte={PopularDiscoverQuality.MinimumVoteCountTv}", query);
    }

    [Fact]
    public void BuildMovieQuery_PopularitySortPreservesUserMinVoteCount()
    {
        var query = TmdbDiscoverQueryBuilder.BuildMovieQuery(CreateCriteria(
            mode: DiscoverBrowseMode.Trending,
            sort: DiscoverBrowseSort.PopularityDesc,
            minVoteCount: 500));

        Assert.Contains("vote_count.gte=500", query);
    }

    [Fact]
    public void BuildMovieQuery_TrendingPopularityAscWithMinVoteCount5000()
    {
        var query = TmdbDiscoverQueryBuilder.BuildMovieQuery(CreateCriteria(
            mode: DiscoverBrowseMode.Trending,
            sort: DiscoverBrowseSort.PopularityAsc,
            minVoteCount: 5000));

        Assert.Contains("sort_by=popularity.asc", query);
        Assert.Contains("vote_count.gte=5000", query);
    }

    [Fact]
    public void BuildTvQuery_TrendingPopularityAscWithMinVoteCount5000()
    {
        var query = TmdbDiscoverQueryBuilder.BuildTvQuery(CreateCriteria(
            mode: DiscoverBrowseMode.Trending,
            sort: DiscoverBrowseSort.PopularityAsc,
            minVoteCount: 5000));

        Assert.Contains("sort_by=popularity.asc", query);
        Assert.Contains("vote_count.gte=5000", query);
    }

    [Fact]
    public void BuildMovieQuery_UserMinVoteCount5000ReachesTmdb()
    {
        var query = TmdbDiscoverQueryBuilder.BuildMovieQuery(CreateCriteria(
            mode: DiscoverBrowseMode.Trending,
            sort: DiscoverBrowseSort.PopularityDesc,
            minVoteCount: 5000));

        Assert.Contains("vote_count.gte=5000", query);
    }

    [Fact]
    public void BuildMovieQuery_RatingDescWithoutUserVoteUsesQualityFloor50()
    {
        var query = TmdbDiscoverQueryBuilder.BuildMovieQuery(CreateCriteria(
            mode: DiscoverBrowseMode.Trending,
            sort: DiscoverBrowseSort.RatingDesc));

        Assert.Contains(
            $"vote_count.gte={DiscoverCatalogSortPolicy.RatingRankedSortMinimumVoteCount}",
            query);
    }

    [Fact]
    public void BuildTvQuery_NewestSortUsesFirstAirDateUpperBound()
    {
        var query = TmdbDiscoverQueryBuilder.BuildTvQuery(CreateCriteria(
            mode: DiscoverBrowseMode.Trending,
            sort: DiscoverBrowseSort.ReleaseDesc));

        var cutoff = DiscoverCatalogSortPolicy.GetFutureReleaseCutoffDate(DateTime.UtcNow);
        Assert.Contains($"first_air_date.lte={cutoff:yyyy-MM-dd}", query);
        Assert.DoesNotContain("primary_release_date", query);
        Assert.DoesNotContain("vote_count.gte", query);
    }

    [Fact]
    public void BuildMovieQuery_NewReleasesAppliesVoteFloorAndRecentWindow()
    {
        var query = TmdbDiscoverQueryBuilder.BuildMovieQuery(CreateCriteria(
            mode: DiscoverBrowseMode.NewReleases,
            sort: DiscoverBrowseSort.ReleaseDesc));

        var today = DateOnly.FromDateTime(DateTime.UtcNow);
        var earliest = today.AddDays(-NewReleasesOptions.DefaultMaxAgeDays);
        Assert.Contains("sort_by=primary_release_date.desc", query);
        Assert.Contains($"vote_count.gte={NewReleasesOptions.DefaultMinVoteCountMovie}", query);
        Assert.Contains($"primary_release_date.gte={earliest:yyyy-MM-dd}", query);
        Assert.Contains($"primary_release_date.lte={today:yyyy-MM-dd}", query);
    }

    [Fact]
    public void BuildTvQuery_NewReleasesUsesLowerVoteFloorAndRecentWindow()
    {
        var query = TmdbDiscoverQueryBuilder.BuildTvQuery(CreateCriteria(
            mode: DiscoverBrowseMode.NewReleases,
            sort: DiscoverBrowseSort.ReleaseDesc));

        var today = DateOnly.FromDateTime(DateTime.UtcNow);
        var earliest = today.AddDays(-NewReleasesOptions.DefaultMaxAgeDays);
        Assert.Contains("sort_by=first_air_date.desc", query);
        Assert.Contains($"vote_count.gte={NewReleasesOptions.DefaultMinVoteCountTv}", query);
        Assert.Contains($"first_air_date.gte={earliest:yyyy-MM-dd}", query);
        Assert.Contains($"first_air_date.lte={today:yyyy-MM-dd}", query);
        Assert.DoesNotContain("primary_release_date", query);
    }

    [Fact]
    public void BuildMovieQuery_NewReleasesKeepsStricterUserVoteCount()
    {
        var query = TmdbDiscoverQueryBuilder.BuildMovieQuery(CreateCriteria(
            mode: DiscoverBrowseMode.NewReleases,
            sort: DiscoverBrowseSort.ReleaseDesc,
            minVoteCount: 500));

        Assert.Contains("vote_count.gte=500", query);
        Assert.DoesNotContain($"vote_count.gte={NewReleasesOptions.DefaultMinVoteCountMovie}", query);
    }

    [Fact]
    public void BuildMovieQuery_NewReleasesRaisesWeakerUserVoteCountToTheFloor()
    {
        var query = TmdbDiscoverQueryBuilder.BuildMovieQuery(CreateCriteria(
            mode: DiscoverBrowseMode.NewReleases,
            sort: DiscoverBrowseSort.ReleaseDesc,
            minVoteCount: 10));

        Assert.Contains($"vote_count.gte={NewReleasesOptions.DefaultMinVoteCountMovie}", query);
        Assert.DoesNotContain("vote_count.gte=10", query);
    }

    [Fact]
    public void BuildMovieQuery_NewReleasesYearFilterSkipsTheAutomaticRecentWindow()
    {
        var query = TmdbDiscoverQueryBuilder.BuildMovieQuery(CreateCriteria(
            mode: DiscoverBrowseMode.NewReleases,
            sort: DiscoverBrowseSort.ReleaseDesc,
            year: 2024));

        var today = DateOnly.FromDateTime(DateTime.UtcNow);
        Assert.Contains("primary_release_year=2024", query);
        Assert.Contains($"primary_release_date.lte={today:yyyy-MM-dd}", query);
        Assert.DoesNotContain("primary_release_date.gte=", query);
        Assert.Contains($"vote_count.gte={NewReleasesOptions.DefaultMinVoteCountMovie}", query);
    }

    [Fact]
    public void BuildMovieQuery_NewReleasesUsesConfiguredFloorAndWindow()
    {
        var constraints = NewReleasesDiscoverConstraints.ForMovies(
            Options.Create(new NewReleasesOptions
            {
                MinVoteCountMovie = 40,
                MaxAgeDays = 30
            }));
        var query = TmdbDiscoverQueryBuilder.BuildMovieQuery(
            CreateCriteria(
                mode: DiscoverBrowseMode.NewReleases,
                sort: DiscoverBrowseSort.ReleaseDesc),
            constraints);

        var today = DateOnly.FromDateTime(DateTime.UtcNow);
        Assert.Contains("vote_count.gte=40", query);
        Assert.Contains($"primary_release_date.gte={today.AddDays(-30):yyyy-MM-dd}", query);
    }

    private static DiscoverProviderCriteria CreateCriteria(
        DiscoverBrowseMode mode = DiscoverBrowseMode.Trending,
        DiscoverBrowseSort? sort = null,
        int? minVoteCount = null,
        int? yearTo = null,
        int? year = null,
        int? yearFrom = null) =>
        new(
            mode,
            Page: 1,
            GenreTmdbIds: [],
            Year: year,
            YearFrom: yearFrom,
            YearTo: yearTo,
            MinRating: null,
            MinVoteCount: minVoteCount,
            MinRuntimeMinutes: null,
            MaxRuntimeMinutes: null,
            Language: null,
            OriginCountry: null,
            KeywordTmdbIds: [],
            TvStatuses: [],
            Sort: sort);
}
