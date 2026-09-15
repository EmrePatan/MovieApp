using Microsoft.EntityFrameworkCore;
using MovieApp.Application.Models.Search;
using MovieApp.Infrastructure.Persistence;
using MovieApp.Infrastructure.Persistence.Repositories;

namespace MovieApp.IntegrationTests.Persistence;

[Collection("CatalogPersistence")]
public sealed class SearchRepositoryIntegrationTests
{
    [Fact]
    public async Task SearchAsyncPaginatesMovieOnlyResultsWithoutDuplicatesOrGaps()
    {
        await using var context = CatalogPersistenceFixture.CreateContext();
        await ClearSearchCatalogAsync(context);

        var today = DateOnly.FromDateTime(DateTime.UtcNow);
        var utcNow = DateTime.UtcNow;
        var movieIds = Enumerable.Range(0, 6)
            .Select(_ => Guid.NewGuid())
            .ToArray();

        context.Movies.AddRange(movieIds.Select((id, index) => new MovieApp.Domain.Entities.Movie
        {
            Id = id,
            Title = $"Alpha Movie {index}",
            ReleaseDate = today.AddDays(index),
            VoteAverage = 8,
            VoteCount = 100 + index,
            CreatedAt = utcNow,
            UpdatedAt = utcNow
        }));
        await context.SaveChangesAsync();

        var repository = new SearchRepository(context);
        var page1 = await repository.SearchAsync(new SearchCriteria(
            "Alpha",
            SearchContentType.Movie,
            null,
            null,
            null,
            null,
            SearchSortOption.TitleAsc,
            1,
            3));
        var page2 = await repository.SearchAsync(new SearchCriteria(
            "Alpha",
            SearchContentType.Movie,
            null,
            null,
            null,
            null,
            SearchSortOption.TitleAsc,
            2,
            3));

        Assert.Equal(6, page1.TotalCount);
        Assert.Equal(3, page1.Items.Count);
        Assert.Equal(3, page2.Items.Count);
        Assert.Equal(6, page1.Items.Concat(page2.Items).Select(item => item.Id).Distinct().Count());
    }

    [Fact]
    public async Task SearchAsyncPaginatesAllContentWithDeterministicOrderingForEqualVotes()
    {
        await using var context = CatalogPersistenceFixture.CreateContext();
        await ClearSearchCatalogAsync(context);

        var utcNow = DateTime.UtcNow;
        var movieA = Guid.Parse("aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaaaaa");
        var movieB = Guid.Parse("bbbbbbbb-bbbb-bbbb-bbbb-bbbbbbbbbbbb");
        var tvA = Guid.Parse("cccccccc-cccc-cccc-cccc-cccccccccccc");
        var tvB = Guid.Parse("dddddddd-dddd-dddd-dddd-dddddddddddd");

        context.Movies.AddRange(
            new MovieApp.Domain.Entities.Movie
            {
                Id = movieB,
                Title = "Shared Vote Movie B",
                VoteAverage = 9,
                VoteCount = 100,
                CreatedAt = utcNow,
                UpdatedAt = utcNow
            },
            new MovieApp.Domain.Entities.Movie
            {
                Id = movieA,
                Title = "Shared Vote Movie A",
                VoteAverage = 9,
                VoteCount = 100,
                CreatedAt = utcNow,
                UpdatedAt = utcNow
            });
        context.TvShows.AddRange(
            new MovieApp.Domain.Entities.TvShow
            {
                Id = tvB,
                Title = "Shared Vote Show B",
                VoteAverage = 9,
                VoteCount = 100,
                Status = MovieApp.Domain.Enums.TvShowStatus.ReturningSeries,
                CreatedAt = utcNow,
                UpdatedAt = utcNow
            },
            new MovieApp.Domain.Entities.TvShow
            {
                Id = tvA,
                Title = "Shared Vote Show A",
                VoteAverage = 9,
                VoteCount = 100,
                Status = MovieApp.Domain.Enums.TvShowStatus.ReturningSeries,
                CreatedAt = utcNow,
                UpdatedAt = utcNow
            });
        await context.SaveChangesAsync();

        var repository = new SearchRepository(context);
        var firstPage = await repository.SearchAsync(new SearchCriteria(
            "Shared Vote",
            SearchContentType.All,
            null,
            null,
            null,
            null,
            SearchSortOption.RatingDesc,
            1,
            10));
        var secondPage = await repository.SearchAsync(new SearchCriteria(
            "Shared Vote",
            SearchContentType.All,
            null,
            null,
            null,
            null,
            SearchSortOption.RatingDesc,
            1,
            10));

        Assert.Equal(
            [movieA, movieB, tvA, tvB],
            firstPage.Items.Select(item => item.Id).ToList());
        Assert.Equal(
            firstPage.Items.Select(item => item.Id).ToList(),
            secondPage.Items.Select(item => item.Id).ToList());
    }

    [Fact]
    public async Task SearchAsyncReturnsEmptyResultsForUnknownQuery()
    {
        await using var context = CatalogPersistenceFixture.CreateContext();
        await ClearSearchCatalogAsync(context);

        var repository = new SearchRepository(context);
        var result = await repository.SearchAsync(new SearchCriteria(
            "does-not-exist",
            SearchContentType.All,
            null,
            null,
            null,
            null,
            SearchSortOption.Relevance,
            1,
            20));

        Assert.Empty(result.Items);
        Assert.Equal(0, result.TotalCount);
    }

    private static async Task ClearSearchCatalogAsync(ApplicationDbContext context)
    {
        context.Movies.RemoveRange(context.Movies);
        context.TvShows.RemoveRange(context.TvShows);
        await context.SaveChangesAsync();
    }
}
