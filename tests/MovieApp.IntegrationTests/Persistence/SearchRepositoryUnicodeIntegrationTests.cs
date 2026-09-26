using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using MovieApp.Application.Configuration;
using MovieApp.Application.Models.Search;
using MovieApp.Infrastructure.Persistence.Repositories;

namespace MovieApp.IntegrationTests.Persistence;

[Collection("CatalogPersistence")]
public sealed class SearchRepositoryUnicodeIntegrationTests
{
    private static SearchRepository CreateRepository(MovieApp.Infrastructure.Persistence.ApplicationDbContext context) =>
        new(context, Options.Create(new TopRatedOptions()), NullLogger<SearchRepository>.Instance);

    [Theory]
    [InlineData("ıslık")]
    [InlineData("Islık")]
    public async Task SearchAsyncFindsTurkishTitleAcrossCaseVariants(string query)
    {
        await using var context = CatalogPersistenceFixture.CreateContext();
        await SearchRepositoryIntegrationTests.ClearSearchCatalogAsync(context);

        var utcNow = DateTime.UtcNow;
        var movieId = Guid.NewGuid();
        context.Movies.Add(new MovieApp.Domain.Entities.Movie
        {
            Id = movieId,
            Title = "Gizli ıslık hikayesi",
            ReleaseDate = DateOnly.FromDateTime(utcNow),
            VoteAverage = 7,
            VoteCount = 10,
            CreatedAt = utcNow,
            UpdatedAt = utcNow,
        });
        await context.SaveChangesAsync();

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
            10));

        Assert.Contains(result.Items, item => item.Id == movieId);
    }

    [Fact]
    public async Task SearchAsyncFindsTvTitleWithTurkishCapitalI()
    {
        await using var context = CatalogPersistenceFixture.CreateContext();
        await SearchRepositoryIntegrationTests.ClearSearchCatalogAsync(context);

        var utcNow = DateTime.UtcNow;
        var tvId = Guid.NewGuid();
        context.TvShows.Add(new MovieApp.Domain.Entities.TvShow
        {
            Id = tvId,
            Title = "İstanbul geceleri",
            VoteAverage = 8,
            VoteCount = 20,
            Status = MovieApp.Domain.Enums.TvShowStatus.ReturningSeries,
            CreatedAt = utcNow,
            UpdatedAt = utcNow,
        });
        await context.SaveChangesAsync();

        var repository = CreateRepository(context);
        var result = await repository.SearchAsync(new SearchCriteria(
            "istanbul",
            SearchContentType.Tv,
            null,
            null,
            null,
            null,
            SearchSortOption.Relevance,
            1,
            10));

        Assert.Contains(result.Items, item => item.Id == tvId);
    }

    [Fact]
    public async Task AutocompleteAsyncFindsTurkishDotlessSubstring()
    {
        await using var context = CatalogPersistenceFixture.CreateContext();
        await SearchRepositoryIntegrationTests.ClearSearchCatalogAsync(context);

        var utcNow = DateTime.UtcNow;
        context.Movies.Add(new MovieApp.Domain.Entities.Movie
        {
            Id = Guid.NewGuid(),
            Title = "Kırmızı ıslık",
            ReleaseDate = DateOnly.FromDateTime(utcNow),
            VoteAverage = 6,
            VoteCount = 5,
            CreatedAt = utcNow,
            UpdatedAt = utcNow,
        });
        await context.SaveChangesAsync();

        var repository = CreateRepository(context);
        var suggestions = await repository.AutocompleteAsync("Islık", 5);

        Assert.Contains(suggestions, suggestion => suggestion.Title.Contains("ıslık", StringComparison.Ordinal));
    }
}
