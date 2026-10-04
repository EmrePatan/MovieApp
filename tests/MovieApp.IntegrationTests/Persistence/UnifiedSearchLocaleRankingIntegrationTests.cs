using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using MovieApp.Application.Configuration;
using MovieApp.Application.Models.Providers;
using MovieApp.Application.Models.Search;
using MovieApp.Domain.Entities;
using MovieApp.Domain.Enums;
using MovieApp.Infrastructure.Persistence;
using MovieApp.Application.Services.Localization;
using MovieApp.Infrastructure.Persistence.Repositories;

namespace MovieApp.IntegrationTests.Persistence;

[Collection("CatalogPersistence")]
public sealed class UnifiedSearchLocaleRankingIntegrationTests
{
    private static SearchRepository CreateRepository(ApplicationDbContext context) =>
        new(context, Options.Create(new TopRatedOptions()), NullLogger<SearchRepository>.Instance);

    [Fact]
    public async Task GermanAliasRemainsSearchableForTurkishLocaleUser()
    {
        await using var context = CatalogPersistenceFixture.CreateContext();
        await SearchRepositoryIntegrationTests.ClearSearchCatalogAsync(context);

        var movieId = await SeedPansLabyrinthFixtureAsync(context);

        var repository = CreateRepository(context);
        var result = await repository.SearchAsync(
            new SearchCriteria(
                "Pan im Labyrinth",
                SearchContentType.Movie,
                null,
                null,
                null,
                null,
                SearchSortOption.Relevance,
                1,
                20),
            ContentLocaleResolver.TurkishTurkey);

        Assert.Equal(1, result.TotalCount);
        Assert.Equal(movieId, result.Items[0].Id);
        Assert.Equal("Pan's Labyrinth", result.Items[0].Title);
        Assert.DoesNotContain("Pan im Labyrinth", result.Items[0].Title, StringComparison.Ordinal);
    }

    [Fact]
    public async Task TurkishAliasRanksAheadOfGermanAliasForSharedExactQuery()
    {
        await using var context = CatalogPersistenceFixture.CreateContext();
        await SearchRepositoryIntegrationTests.ClearSearchCatalogAsync(context);

        const string sharedExactTitle = "LocaleRank Shared Exact Title";
        var turkishPreferredId = Guid.Parse("aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaaaaa");
        var germanCompetitorId = Guid.Parse("bbbbbbbb-bbbb-bbbb-bbbb-bbbbbbbbbbbb");
        var utcNow = DateTime.UtcNow;

        context.Movies.AddRange(
            CreateMovie(turkishPreferredId, "Turkish Preferred Canonical", utcNow),
            CreateMovie(germanCompetitorId, "German Competitor Canonical", utcNow));
        await context.SaveChangesAsync();

        var synchronizer = new ContentSearchTitleSynchronizer(context);
        await synchronizer.SyncFromProviderDetailAsync(
            CatalogContentType.Movie,
            turkishPreferredId,
            "Turkish Preferred Canonical",
            "Original TR",
            [
                new ProviderSearchTitleEntry(
                    sharedExactTitle,
                    ContentSearchTitleKind.Translation,
                    ContentSearchTitleSource.TmdbTranslation,
                    "tr",
                    "TR",
                    null),
            ],
            utcNow);
        await synchronizer.SyncFromProviderDetailAsync(
            CatalogContentType.Movie,
            germanCompetitorId,
            "German Competitor Canonical",
            "Original DE",
            [
                new ProviderSearchTitleEntry(
                    sharedExactTitle,
                    ContentSearchTitleKind.Translation,
                    ContentSearchTitleSource.TmdbTranslation,
                    "de",
                    "DE",
                    null),
            ],
            utcNow);

        var repository = CreateRepository(context);
        var result = await repository.SearchAsync(
            new SearchCriteria(
                sharedExactTitle,
                SearchContentType.Movie,
                null,
                null,
                null,
                null,
                SearchSortOption.Relevance,
                1,
                20),
            ContentLocaleResolver.TurkishTurkey);

        Assert.Equal(2, result.TotalCount);
        Assert.Equal(turkishPreferredId, result.Items[0].Id);
        Assert.Equal(germanCompetitorId, result.Items[1].Id);
    }

    [Theory]
    [InlineData("Pan'ın Labirenti")]
    [InlineData("Pan's Labyrinth")]
    [InlineData("El laberinto del fauno")]
    public async Task MultiLocaleAliasesFindPansLabyrinthForTurkishUser(string query)
    {
        await using var context = CatalogPersistenceFixture.CreateContext();
        await SearchRepositoryIntegrationTests.ClearSearchCatalogAsync(context);

        var movieId = await SeedPansLabyrinthFixtureAsync(context);

        var repository = CreateRepository(context);
        var result = await repository.SearchAsync(
            new SearchCriteria(
                query,
                SearchContentType.Movie,
                null,
                null,
                null,
                null,
                SearchSortOption.Relevance,
                1,
                20),
            ContentLocaleResolver.TurkishTurkey);

        Assert.Equal(1, result.TotalCount);
        Assert.Equal(movieId, result.Items[0].Id);
        Assert.Equal("Pan's Labyrinth", result.Items[0].Title);
        Assert.DoesNotContain("Pan im Labyrinth", result.Items[0].Title, StringComparison.Ordinal);
    }

    private static Movie CreateMovie(Guid id, string title, DateTime utcNow) =>
        new()
        {
            Id = id,
            Title = title,
            OriginalTitle = "Original",
            ReleaseDate = DateOnly.FromDateTime(utcNow),
            VoteAverage = 8m,
            VoteCount = 500,
            CreatedAt = utcNow,
            UpdatedAt = utcNow,
        };

    private static async Task<Guid> SeedPansLabyrinthFixtureAsync(ApplicationDbContext context)
    {
        var movieId = Guid.Parse("33333333-3333-3333-3333-333333333333");
        var utcNow = DateTime.UtcNow;

        context.Movies.Add(new Movie
        {
            Id = movieId,
            Title = "Pan's Labyrinth",
            OriginalTitle = "El laberinto del fauno",
            ReleaseDate = DateOnly.FromDateTime(utcNow),
            VoteAverage = 8.2m,
            VoteCount = 12_000,
            CreatedAt = utcNow,
            UpdatedAt = utcNow,
        });
        await context.SaveChangesAsync();

        var synchronizer = new ContentSearchTitleSynchronizer(context);
        await synchronizer.SyncFromProviderDetailAsync(
            CatalogContentType.Movie,
            movieId,
            "Pan's Labyrinth",
            "El laberinto del fauno",
            [
                new ProviderSearchTitleEntry(
                    "Pan'ın Labirenti",
                    ContentSearchTitleKind.Translation,
                    ContentSearchTitleSource.TmdbTranslation,
                    "tr",
                    "TR",
                    null),
                new ProviderSearchTitleEntry(
                    "Pan im Labyrinth",
                    ContentSearchTitleKind.Translation,
                    ContentSearchTitleSource.TmdbTranslation,
                    "de",
                    "DE",
                    null),
            ],
            utcNow);

        return movieId;
    }
}
