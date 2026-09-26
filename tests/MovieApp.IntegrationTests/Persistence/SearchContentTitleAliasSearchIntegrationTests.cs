using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using MovieApp.Application.Configuration;
using MovieApp.Application.Models.Providers;
using MovieApp.Application.Models.Search;
using MovieApp.Domain.Entities;
using MovieApp.Domain.Enums;
using MovieApp.Infrastructure.Persistence;
using MovieApp.Infrastructure.Persistence.Repositories;

namespace MovieApp.IntegrationTests.Persistence;

[Collection("CatalogPersistence")]
public sealed class SearchContentTitleAliasSearchIntegrationTests
{
    private static readonly string[] TargetSearchQueries =
    [
        "dönersen ısl",
        "dönersen isl",
        "donersen isl",
        "islik",
        "ıslık",
        "whistle if you",
        "WHISTLE IF YOU",
    ];

    private static readonly string[] TurkishAutocompletePrefixQueries =
    [
        "dönersen",
        "dönersen ı",
        "dönersen ıs",
        "dönersen ısl",
        "dönersen ıslı",
        "dönersen ıslık",
        "dönersen isl",
        "donersen isl",
        "islik",
        "ıslık",
        "whistle if you",
    ];

    private static SearchRepository CreateRepository(ApplicationDbContext context) =>
        new(context, Options.Create(new TopRatedOptions()), NullLogger<SearchRepository>.Instance);

    [Theory]
    [MemberData(nameof(MovieSearchQueryCases))]
    public async Task MovieSearchResolvesWhistleFixtureByAliasOrCanonicalTitle(string query)
    {
        await using var context = CatalogPersistenceFixture.CreateContext();
        await SearchRepositoryIntegrationTests.ClearSearchCatalogAsync(context);

        var movieId = await SeedWhistleMovieAsync(context);

        var repository = CreateRepository(context);
        var result = await repository.SearchAsync(new SearchCriteria(
            query,
            SearchContentType.Movie,
            null,
            null,
            null,
            null,
            SearchSortOption.Relevance,
            1,
            20));

        Assert.Equal(1, result.TotalCount);
        Assert.Single(result.Items);
        Assert.Equal(movieId, result.Items[0].Id);
        Assert.Equal("Whistle If You Come Back", result.Items[0].Title);
        Assert.DoesNotContain(result.Items, item =>
            item.Title.Contains("donersen", StringComparison.OrdinalIgnoreCase));
    }

    [Theory]
    [MemberData(nameof(MovieSearchQueryCases))]
    public async Task TvSearchResolvesLocalizedAliasFixture(string query)
    {
        await using var context = CatalogPersistenceFixture.CreateContext();
        await SearchRepositoryIntegrationTests.ClearSearchCatalogAsync(context);

        var tvId = await SeedWhistleTvShowAsync(context);

        var repository = CreateRepository(context);
        var result = await repository.SearchAsync(new SearchCriteria(
            query,
            SearchContentType.Tv,
            null,
            null,
            null,
            null,
            SearchSortOption.Relevance,
            1,
            20));

        Assert.Equal(1, result.TotalCount);
        Assert.Single(result.Items);
        Assert.Equal(tvId, result.Items[0].Id);
        Assert.Equal("Whistle If You Come Back", result.Items[0].Title);
    }

    [Theory]
    [MemberData(nameof(TurkishAutocompleteQueryCases))]
    public async Task LocalAutocompleteReturnsCanonicalDisplayTitleForAliasMatches(string query)
    {
        await using var context = CatalogPersistenceFixture.CreateContext();
        await SearchRepositoryIntegrationTests.ClearSearchCatalogAsync(context);

        var movieId = await SeedWhistleMovieAsync(context);

        var repository = CreateRepository(context);
        var suggestions = await repository.AutocompleteAsync(query, 10);

        Assert.Contains(suggestions, s => s.Id == movieId && s.Title == "Whistle If You Come Back");
        Assert.DoesNotContain(suggestions, s => s.Title.Contains("donersen", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public async Task SearchTotalCountDedupesMultipleMatchingAliasRows()
    {
        await using var context = CatalogPersistenceFixture.CreateContext();
        await SearchRepositoryIntegrationTests.ClearSearchCatalogAsync(context);

        var movieId = await SeedWhistleMovieAsync(context);
        var synchronizer = new ContentSearchTitleSynchronizer(context);
        var utcNow = DateTime.UtcNow;

        await synchronizer.SyncFromProviderDetailAsync(
            CatalogContentType.Movie,
            movieId,
            "Whistle If You Come Back",
            "Original EN",
            [
                new ProviderSearchTitleEntry(
                    "Extra Islik Alias One",
                    ContentSearchTitleKind.Alternative,
                    ContentSearchTitleSource.TmdbAlternative,
                    null,
                    "TR",
                    null),
                new ProviderSearchTitleEntry(
                    "Extra Islik Alias Two",
                    ContentSearchTitleKind.Alternative,
                    ContentSearchTitleSource.TmdbAlternative,
                    null,
                    "TR",
                    null),
                new ProviderSearchTitleEntry(
                    "Extra Islik Alias Three",
                    ContentSearchTitleKind.Alternative,
                    ContentSearchTitleSource.TmdbAlternative,
                    null,
                    "TR",
                    null),
            ],
            utcNow);

        var repository = CreateRepository(context);
        var result = await repository.SearchAsync(new SearchCriteria(
            "islik",
            SearchContentType.Movie,
            null,
            null,
            null,
            null,
            SearchSortOption.Relevance,
            1,
            20));

        Assert.Equal(1, result.TotalCount);
        Assert.Single(result.Items);
        Assert.Equal(movieId, result.Items[0].Id);
    }

    public static IEnumerable<object[]> MovieSearchQueryCases() =>
        TargetSearchQueries.Select(query => new object[] { query });

    public static IEnumerable<object[]> TurkishAutocompleteQueryCases() =>
        TurkishAutocompletePrefixQueries.Select(query => new object[] { query });

    private static async Task<Guid> SeedWhistleMovieAsync(ApplicationDbContext context)
    {
        var movieId = Guid.Parse("11111111-1111-1111-1111-111111111111");
        var utcNow = DateTime.UtcNow;

        context.Movies.Add(new Movie
        {
            Id = movieId,
            Title = "Whistle If You Come Back",
            OriginalTitle = "Original EN",
            ReleaseDate = DateOnly.FromDateTime(utcNow),
            VoteAverage = 7.5m,
            VoteCount = 120,
            CreatedAt = utcNow,
            UpdatedAt = utcNow,
        });
        await context.SaveChangesAsync();

        var synchronizer = new ContentSearchTitleSynchronizer(context);
        await synchronizer.SyncFromProviderDetailAsync(
            CatalogContentType.Movie,
            movieId,
            "Whistle If You Come Back",
            "Original EN",
            [
                new ProviderSearchTitleEntry(
                    "Dönersen Islık Çal",
                    ContentSearchTitleKind.Alternative,
                    ContentSearchTitleSource.TmdbAlternative,
                    null,
                    "TR",
                    "working"),
            ],
            utcNow);

        return movieId;
    }

    private static async Task<Guid> SeedWhistleTvShowAsync(ApplicationDbContext context)
    {
        var tvId = Guid.Parse("22222222-2222-2222-2222-222222222222");
        var utcNow = DateTime.UtcNow;

        context.TvShows.Add(new TvShow
        {
            Id = tvId,
            Title = "Whistle If You Come Back",
            OriginalTitle = "Original EN",
            FirstAirDate = DateOnly.FromDateTime(utcNow),
            VoteAverage = 7.5m,
            VoteCount = 120,
            Status = TvShowStatus.ReturningSeries,
            CreatedAt = utcNow,
            UpdatedAt = utcNow,
        });
        await context.SaveChangesAsync();

        var synchronizer = new ContentSearchTitleSynchronizer(context);
        await synchronizer.SyncFromProviderDetailAsync(
            CatalogContentType.Tv,
            tvId,
            "Whistle If You Come Back",
            "Original EN",
            [
                new ProviderSearchTitleEntry(
                    "Dönersen Islık Çal",
                    ContentSearchTitleKind.Alternative,
                    ContentSearchTitleSource.TmdbAlternative,
                    null,
                    "TR",
                    "working"),
            ],
            utcNow);

        return tvId;
    }
}
