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
    }

    [Fact]
    public void BuildMovieQuery_RatingDescAppliesMinimumVoteFloor()
    {
        var query = TmdbDiscoverQueryBuilder.BuildMovieQuery(CreateCriteria(
            mode: DiscoverBrowseMode.Trending,
            sort: DiscoverBrowseSort.RatingDesc));

        Assert.Contains(
            $"vote_count.gte={DiscoverCatalogSortPolicy.TopRatedMinimumVoteCount}",
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
    public void BuildTvQuery_NewestSortUsesFirstAirDateUpperBound()
    {
        var query = TmdbDiscoverQueryBuilder.BuildTvQuery(CreateCriteria(
            mode: DiscoverBrowseMode.Trending,
            sort: DiscoverBrowseSort.ReleaseDesc));

        var cutoff = DiscoverCatalogSortPolicy.GetFutureReleaseCutoffDate(DateTime.UtcNow);
        Assert.Contains($"first_air_date.lte={cutoff:yyyy-MM-dd}", query);
        Assert.DoesNotContain("primary_release_date", query);
    }

    private static DiscoverProviderCriteria CreateCriteria(
        DiscoverBrowseMode mode = DiscoverBrowseMode.Trending,
        DiscoverBrowseSort? sort = null,
        int? minVoteCount = null,
        int? yearTo = null) =>
        new(
            mode,
            Page: 1,
            GenreTmdbIds: [],
            Year: null,
            YearFrom: null,
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
