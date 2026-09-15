using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using MovieApp.Application.Configuration;
using MovieApp.Application.Models.Search;
using MovieApp.Infrastructure.Persistence;
using MovieApp.Infrastructure.Persistence.Repositories;

namespace MovieApp.IntegrationTests.Persistence;

[Collection("CatalogPersistence")]
public sealed class SearchRepositoryIntegrationTests
{
    private static SearchRepository CreateRepository(ApplicationDbContext context) =>
        new(context, Options.Create(new TopRatedOptions()));

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

        var repository = CreateRepository(context);
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

        var repository = CreateRepository(context);
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

        var repository = CreateRepository(context);
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

    [Fact]
    public async Task GetTrendingAsyncExecutesMixedMovieTvAndPersonSetOperationWithoutTranslationFailure()
    {
        await using var context = CatalogPersistenceFixture.CreateContext();
        await ClearSearchCatalogAsync(context);

        var utcNow = DateTime.UtcNow;
        var movieId = Guid.Parse("11111111-1111-1111-1111-111111111111");
        var tvId = Guid.Parse("22222222-2222-2222-2222-222222222222");
        var personId = Guid.Parse("33333333-3333-3333-3333-333333333333");

        context.Movies.Add(new MovieApp.Domain.Entities.Movie
        {
            Id = movieId,
            Title = "Trending Movie",
            TmdbId = 1001,
            VoteAverage = 8.5m,
            VoteCount = 500,
            CreatedAt = utcNow,
            UpdatedAt = utcNow
        });
        context.TvShows.Add(new MovieApp.Domain.Entities.TvShow
        {
            Id = tvId,
            Title = "Trending Show",
            TmdbId = 2002,
            VoteAverage = 8.0m,
            VoteCount = 400,
            Status = MovieApp.Domain.Enums.TvShowStatus.ReturningSeries,
            CreatedAt = utcNow,
            UpdatedAt = utcNow
        });
        context.People.Add(new MovieApp.Domain.Entities.Person
        {
            Id = personId,
            Name = "Trending Person",
            TmdbId = 3003,
            CreatedAt = utcNow,
            UpdatedAt = utcNow
        });
        await context.SaveChangesAsync();

        var repository = CreateRepository(context);
        var result = await repository.GetTrendingAsync(new DiscoveryCriteria(SearchContentType.All, 1, 10));

        Assert.Equal(3, result.TotalCount);
        Assert.Contains(result.Items, item => item.Type == "movie" && item.Title == "Trending Movie");
        Assert.Contains(result.Items, item => item.Type == "tv" && item.Title == "Trending Show");
        Assert.Contains(result.Items, item => item.Type == "person" && item.Title == "Trending Person");
    }

    private static async Task ClearSearchCatalogAsync(ApplicationDbContext context)
    {
        context.Movies.RemoveRange(context.Movies);
        context.TvShows.RemoveRange(context.TvShows);
        context.People.RemoveRange(context.People);
        await context.SaveChangesAsync();
    }
}
