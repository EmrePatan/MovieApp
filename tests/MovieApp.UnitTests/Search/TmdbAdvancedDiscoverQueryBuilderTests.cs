using MovieApp.Application.Models.Providers;
using MovieApp.Application.Models.Search;
using MovieApp.Infrastructure.Providers.Tmdb;

namespace MovieApp.UnitTests.Search;

public sealed class TmdbAdvancedDiscoverQueryBuilderTests
{
    [Fact]
    public void BuildMovieQueryIncludesAdultFalseAndMovieDateFields()
    {
        var query = TmdbAdvancedDiscoverQueryBuilder.BuildMovieQuery(CreateCriteria(
            yearFrom: 2020,
            yearTo: 2024,
            sort: AdvancedDiscoverSort.Newest));

        Assert.Contains("include_adult=false", query);
        Assert.Contains("primary_release_date.gte=2020-01-01", query);
        Assert.Contains("primary_release_date.lte=2024-12-31", query);
        Assert.Contains("sort_by=primary_release_date.desc", query);
        Assert.DoesNotContain("first_air_date", query);
    }

    [Fact]
    public void BuildTvQueryUsesTvDateAndSortFields()
    {
        var query = TmdbAdvancedDiscoverQueryBuilder.BuildTvQuery(CreateCriteria(
            year: 2023,
            sort: AdvancedDiscoverSort.Oldest));

        Assert.Contains("include_adult=false", query);
        Assert.Contains("first_air_date_year=2023", query);
        Assert.Contains("sort_by=first_air_date.asc", query);
        Assert.DoesNotContain("primary_release", query);
    }

    [Fact]
    public void BuildMovieQueryMapsGenreRatingVoteRuntimeLanguageAndOriginCountry()
    {
        var query = TmdbAdvancedDiscoverQueryBuilder.BuildMovieQuery(CreateCriteria(
            genreTmdbIds: [28, 12],
            minRating: 7.5m,
            maxRating: 9m,
            minVoteCount: 500,
            minRuntimeMinutes: 90,
            maxRuntimeMinutes: 150,
            originalLanguage: "en",
            originCountry: "US",
            sort: AdvancedDiscoverSort.RatingDesc));

        Assert.Contains("with_genres=28,12", query);
        Assert.Contains("vote_average.gte=7.5", query);
        Assert.Contains("vote_average.lte=9", query);
        Assert.Contains("vote_count.gte=500", query);
        Assert.Contains("with_runtime.gte=90", query);
        Assert.Contains("with_runtime.lte=150", query);
        Assert.Contains("with_original_language=en", query);
        Assert.Contains("with_origin_country=US", query);
        Assert.Contains("sort_by=vote_average.desc", query);
    }

    [Fact]
    public void BuildMovieQueryUsesPipeDelimiterForGenreAnyMatch()
    {
        var query = TmdbAdvancedDiscoverQueryBuilder.BuildMovieQuery(CreateCriteria(
            genreTmdbIds: [28, 12],
            genreMatch: GenreMatchMode.Any));

        Assert.Contains("with_genres=28|12", query);
    }

    [Fact]
    public void BuildMovieQueryMapsCertificationAndReleaseTypes()
    {
        var query = TmdbAdvancedDiscoverQueryBuilder.BuildMovieQuery(CreateCriteria(
            certification: "PG-13",
            certificationCountry: "US",
            releaseTypes: [DiscoverReleaseType.Theatrical, DiscoverReleaseType.Digital]));

        Assert.Contains("certification=PG-13", query);
        Assert.Contains("certification_country=US", query);
        Assert.Contains("with_release_type=3|4", query);
    }

    [Fact]
    public void BuildMovieQueryMapsWatchRegionProvidersAndMonetizationWithOrDelimiter()
    {
        var query = TmdbAdvancedDiscoverQueryBuilder.BuildMovieQuery(CreateCriteria(
            watchRegion: "TR",
            watchProviderIds: [337, 8],
            watchMonetizationTypes: [WatchMonetizationType.Stream, WatchMonetizationType.Free]));

        Assert.Contains("watch_region=TR", query);
        Assert.Contains("with_watch_providers=8|337", query);
        Assert.Contains("with_watch_monetization_types=flatrate|free", query);
    }

    [Fact]
    public void BuildMovieQueryKeepsOriginCountryIndependentFromWatchRegion()
    {
        var query = TmdbAdvancedDiscoverQueryBuilder.BuildMovieQuery(CreateCriteria(
            originCountry: "KR",
            watchRegion: "TR",
            watchProviderIds: [8],
            watchMonetizationTypes: [WatchMonetizationType.Stream]));

        Assert.Contains("with_origin_country=KR", query);
        Assert.Contains("watch_region=TR", query);
    }

    [Theory]
    [InlineData(AdvancedDiscoverSort.PopularityDesc, "popularity.desc")]
    [InlineData(AdvancedDiscoverSort.RatingDesc, "vote_average.desc")]
    [InlineData(AdvancedDiscoverSort.Newest, "primary_release_date.desc")]
    [InlineData(AdvancedDiscoverSort.Oldest, "primary_release_date.asc")]
    public void BuildMovieQueryMapsSupportedSorts(AdvancedDiscoverSort sort, string expectedSortBy)
    {
        var query = TmdbAdvancedDiscoverQueryBuilder.BuildMovieQuery(CreateCriteria(sort: sort));

        Assert.Contains($"sort_by={expectedSortBy}", query);
    }

    private static AdvancedDiscoverProviderCriteria CreateCriteria(
        int page = 1,
        IReadOnlyList<int>? genreTmdbIds = null,
        GenreMatchMode genreMatch = GenreMatchMode.All,
        int? year = null,
        int? yearFrom = null,
        int? yearTo = null,
        decimal? minRating = null,
        decimal? maxRating = null,
        int? minVoteCount = null,
        int? minRuntimeMinutes = null,
        int? maxRuntimeMinutes = null,
        string? originalLanguage = null,
        string? originCountry = null,
        string? certification = null,
        string? certificationCountry = null,
        IReadOnlyList<DiscoverReleaseType>? releaseTypes = null,
        string? watchRegion = null,
        IReadOnlyList<int>? watchProviderIds = null,
        IReadOnlyList<WatchMonetizationType>? watchMonetizationTypes = null,
        AdvancedDiscoverSort sort = AdvancedDiscoverSort.PopularityDesc) =>
        new(
            page,
            genreTmdbIds ?? [],
            genreMatch,
            year,
            yearFrom,
            yearTo,
            minRating,
            maxRating,
            minVoteCount,
            minRuntimeMinutes,
            maxRuntimeMinutes,
            originalLanguage,
            originCountry,
            certification,
            certificationCountry,
            releaseTypes ?? [],
            watchRegion,
            watchProviderIds ?? [],
            watchMonetizationTypes ?? [],
            sort);
}
