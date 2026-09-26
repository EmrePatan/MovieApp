using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using MovieApp.Application.Configuration;
using MovieApp.Application.Models.Search;
using MovieApp.Infrastructure.Persistence.Repositories;

namespace MovieApp.IntegrationTests.Persistence;

[Collection("CatalogPersistence")]
public sealed class SearchAutocompleteTurkishIncrementalIntegrationTests
{
    private static readonly string[] LocalCatalogPrefixQueries =
    [
        "dönersen",
        "dönersen ı",
        "dönersen ıs",
        "dönersen ısl",
        "dönersen ıslı",
        "dönersen ıslık",
    ];

    [Theory]
    [MemberData(nameof(IncrementalQueryCases))]
    public async Task LocalAutocompleteFindsCatalogTitleForIncrementalTurkishPrefixes(string query)
    {
        await using var context = CatalogPersistenceFixture.CreateContext();
        await SearchRepositoryIntegrationTests.ClearSearchCatalogAsync(context);

        var utcNow = DateTime.UtcNow;
        var movieId = Guid.NewGuid();
        context.Movies.Add(new MovieApp.Domain.Entities.Movie
        {
            Id = movieId,
            Title = "Dönersen ıslık",
            ReleaseDate = DateOnly.FromDateTime(utcNow),
            VoteAverage = 7,
            VoteCount = 50,
            CreatedAt = utcNow,
            UpdatedAt = utcNow,
        });
        await context.SaveChangesAsync();

        var repository = new SearchRepository(
            context,
            Options.Create(new TopRatedOptions()),
            NullLogger<SearchRepository>.Instance);

        var suggestions = await repository.AutocompleteAsync(query, 10);

        Assert.Contains(suggestions, suggestion => suggestion.Id == movieId);
    }

    public static IEnumerable<object[]> IncrementalQueryCases() =>
        LocalCatalogPrefixQueries.Select(query => new object[] { query });

    [Theory]
    [InlineData("dönersen I")]
    [InlineData("dönersen is")]
    public async Task LocalAutocompleteMatchesAsciiSuffixOnMixedTitle(string query)
    {
        await using var context = CatalogPersistenceFixture.CreateContext();
        await SearchRepositoryIntegrationTests.ClearSearchCatalogAsync(context);

        var utcNow = DateTime.UtcNow;
        var movieId = Guid.NewGuid();
        context.Movies.Add(new MovieApp.Domain.Entities.Movie
        {
            Id = movieId,
            Title = "Dönersen Islik",
            ReleaseDate = DateOnly.FromDateTime(utcNow),
            VoteAverage = 7,
            VoteCount = 50,
            CreatedAt = utcNow,
            UpdatedAt = utcNow,
        });
        await context.SaveChangesAsync();

        var repository = new SearchRepository(
            context,
            Options.Create(new TopRatedOptions()),
            NullLogger<SearchRepository>.Instance);

        var suggestions = await repository.AutocompleteAsync(query, 10);

        Assert.Contains(suggestions, suggestion => suggestion.Id == movieId);
    }
}
