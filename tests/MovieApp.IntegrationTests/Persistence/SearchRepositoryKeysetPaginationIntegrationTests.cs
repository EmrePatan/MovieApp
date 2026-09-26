using System.Data.Common;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using MovieApp.IntegrationTests;
using Microsoft.Extensions.Logging.Abstractions;
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
        new(context, Options.Create(new TopRatedOptions()), NullLogger<SearchRepository>.Instance);

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
        Assert.False(page3.HasNextPage);
        Assert.Equal(25, page1.TotalCount);
        Assert.Equal(25, page2.TotalCount);
        Assert.Equal(25, page3.TotalCount);

        var allIds = page1.Items.Concat(page2.Items).Concat(page3.Items).Select(item => item.Id).ToList();
        Assert.Equal(25, allIds.Count);
        Assert.Equal(25, allIds.Distinct().Count());
    }

    [Fact]
    public async Task SearchAsyncCursorContinuationSkipsCountQuery()
    {
        await using var context = CreateInstrumentedContext(out var interceptor);
        await ClearSearchCatalogAsync(context);

        var utcNow = DateTime.UtcNow;
        for (var index = 0; index < 25; index++)
        {
            context.Movies.Add(new MovieApp.Domain.Entities.Movie
            {
                Id = Guid.NewGuid(),
                Title = $"Count Skip {index:D2}",
                VoteAverage = 8,
                VoteCount = 500 - index,
                CreatedAt = utcNow,
                UpdatedAt = utcNow
            });
        }

        await context.SaveChangesAsync();

        var repository = CreateRepository(context);
        var criteria = new SearchCriteria(
            "Count Skip",
            SearchContentType.Movie,
            null,
            null,
            null,
            null,
            SearchSortOption.RatingDesc,
            1,
            10);

        interceptor.Reset();
        var page1 = await repository.SearchAsync(criteria);
        Assert.Equal(1, interceptor.CountQueryCount);
        Assert.Equal(10, page1.Items.Count);

        interceptor.Reset();
        var page2 = await repository.SearchAsync(criteria with { Cursor = page1.NextCursor, Page = 1 });
        Assert.Equal(0, interceptor.CountQueryCount);
        Assert.Equal(10, page2.Items.Count);
        Assert.Equal(page1.TotalCount, page2.TotalCount);
    }

    [Fact]
    public async Task SearchAsyncPageSizeProbeDoesNotReturnExtraItem()
    {
        await using var context = CatalogPersistenceFixture.CreateContext();
        await ClearSearchCatalogAsync(context);

        var utcNow = DateTime.UtcNow;
        context.Movies.Add(new MovieApp.Domain.Entities.Movie
        {
            Id = Guid.NewGuid(),
            Title = "Single Match",
            VoteAverage = 5,
            VoteCount = 1,
            CreatedAt = utcNow,
            UpdatedAt = utcNow
        });
        await context.SaveChangesAsync();

        var repository = CreateRepository(context);
        var result = await repository.SearchAsync(new SearchCriteria(
            "Single",
            SearchContentType.Movie,
            null,
            null,
            null,
            null,
            SearchSortOption.Relevance,
            1,
            20));

        Assert.Single(result.Items);
        Assert.False(result.HasNextPage);
        Assert.Null(result.NextCursor);
    }

    [Fact]
    public async Task SearchAsyncLegacyOffsetPageStillExecutesCount()
    {
        await using var context = CreateInstrumentedContext(out var interceptor);
        await ClearSearchCatalogAsync(context);

        var utcNow = DateTime.UtcNow;
        for (var index = 0; index < 25; index++)
        {
            context.Movies.Add(new MovieApp.Domain.Entities.Movie
            {
                Id = Guid.NewGuid(),
                Title = $"Legacy Page {index}",
                VoteAverage = 8,
                VoteCount = 100 - index,
                CreatedAt = utcNow,
                UpdatedAt = utcNow
            });
        }

        await context.SaveChangesAsync();

        var repository = CreateRepository(context);
        interceptor.Reset();
        var page2 = await repository.SearchAsync(new SearchCriteria(
            "Legacy Page",
            SearchContentType.Movie,
            null,
            null,
            null,
            null,
            SearchSortOption.RatingDesc,
            2,
            10));

        Assert.Equal(1, interceptor.CountQueryCount);
        Assert.Equal(10, page2.Items.Count);
        Assert.Equal(25, page2.TotalCount);
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

    private static ApplicationDbContext CreateInstrumentedContext(out SearchCountQueryInterceptor interceptor)
    {
        interceptor = new SearchCountQueryInterceptor();
        var options = new DbContextOptionsBuilder<ApplicationDbContext>()
            .UseNpgsql(
                IntegrationTestDatabase.GetConnectionString(),
                npgsqlOptions => npgsqlOptions.EnableRetryOnFailure(maxRetryCount: 3))
            .AddInterceptors(interceptor)
            .Options;

        return new ApplicationDbContext(options);
    }

    private sealed class SearchCountQueryInterceptor : DbCommandInterceptor
    {
        public int CountQueryCount { get; private set; }

        public void Reset() => CountQueryCount = 0;

        public override DbDataReader ReaderExecuted(
            DbCommand command,
            CommandExecutedEventData eventData,
            DbDataReader result)
        {
            if (IsCountQuery(command.CommandText))
            {
                CountQueryCount++;
            }

            return base.ReaderExecuted(command, eventData, result);
        }

        public override ValueTask<DbDataReader> ReaderExecutedAsync(
            DbCommand command,
            CommandExecutedEventData eventData,
            DbDataReader result,
            CancellationToken cancellationToken = default)
        {
            if (IsCountQuery(command.CommandText))
            {
                CountQueryCount++;
            }

            return base.ReaderExecutedAsync(command, eventData, result, cancellationToken);
        }

        private static bool IsCountQuery(string commandText) =>
            commandText.Contains("count(", StringComparison.OrdinalIgnoreCase);
    }
}
