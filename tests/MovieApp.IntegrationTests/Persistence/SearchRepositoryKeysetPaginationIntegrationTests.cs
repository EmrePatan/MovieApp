using Microsoft.Extensions.Options;
using MovieApp.Application.Configuration;
using MovieApp.Application.Models.Search;
using MovieApp.Infrastructure.Persistence;
using MovieApp.Infrastructure.Persistence.Repositories;

namespace MovieApp.IntegrationTests.Persistence;

[Collection("CatalogPersistence")]
public sealed class SearchRepositoryKeysetPaginationIntegrationTests
{
    private static SearchRepository CreateRepository(ApplicationDbContext context) =>
        new(context, Options.Create(new TopRatedOptions()));

    [Fact]
    public async Task SearchAsyncKeysetPagesAreContiguousWithoutDuplicates()
    {
        await using var context = CatalogPersistenceFixture.CreateContext();
        await ClearSearchCatalogAsync(context);

        var utcNow = DateTime.UtcNow;
        for (var index = 0; index < 25; index++)
        {
            context.Movies.Add(new MovieApp.Domain.Entities.Movie
            {
                Id = Guid.NewGuid(),
                Title = $"Alpha Keyset {index:D2}",
                VoteAverage = 8,
                VoteCount = 1000 - index,
                CreatedAt = utcNow,
                UpdatedAt = utcNow
            });
        }

        await context.SaveChangesAsync();

        var repository = CreateRepository(context);
        const int pageSize = 10;
        var criteria = new SearchCriteria(
            "Alpha Keyset",
            SearchContentType.Movie,
            null,
            null,
            null,
            null,
            SearchSortOption.RatingDesc,
            1,
            pageSize);

        var page1 = await repository.SearchAsync(criteria);
        Assert.Equal(10, page1.Items.Count);
        Assert.NotNull(page1.NextCursor);

        var page2 = await repository.SearchAsync(criteria with { Cursor = page1.NextCursor, Page = 1 });
        Assert.Equal(10, page2.Items.Count);
        Assert.Equal(2, page2.Page);
        Assert.NotNull(page2.NextCursor);

        var page3 = await repository.SearchAsync(criteria with { Cursor = page2.NextCursor, Page = 1 });
        Assert.Equal(5, page3.Items.Count);
        Assert.Null(page3.NextCursor);

        var allIds = page1.Items.Concat(page2.Items).Concat(page3.Items).Select(item => item.Id).ToList();
        Assert.Equal(25, allIds.Count);
        Assert.Equal(25, allIds.Distinct().Count());
    }

    [Fact]
    public async Task SearchAsyncMalformedCursorIsRejectedByValidator()
    {
        var criteria = new SearchCriteria(
            "alpha",
            SearchContentType.Movie,
            null,
            null,
            null,
            null,
            SearchSortOption.Relevance,
            1,
            20,
            Cursor: "not-a-valid-cursor");

        var validation = MovieApp.Application.Validation.AdvancedSearchValidator.Validate(criteria);
        Assert.False(validation.IsValid);
    }

    private static async Task ClearSearchCatalogAsync(ApplicationDbContext context)
    {
        context.Movies.RemoveRange(context.Movies);
        context.TvShows.RemoveRange(context.TvShows);
        context.People.RemoveRange(context.People);
        await context.SaveChangesAsync();
    }
}
