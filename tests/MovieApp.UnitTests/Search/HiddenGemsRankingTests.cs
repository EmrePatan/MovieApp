using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using MovieApp.Application.Configuration;
using MovieApp.Application.Models.Search;
using MovieApp.Application.Services.Search;
using MovieApp.Domain.Entities;
using MovieApp.Infrastructure.Persistence;
using MovieApp.Infrastructure.Persistence.Repositories;
using MovieApp.Infrastructure.Persistence.Search;

namespace MovieApp.UnitTests.Search;

public sealed class HiddenGemsRankingTests
{
    [Fact]
    public async Task GetHiddenGemsAsyncPrefersHigherConfidenceTitleOverRawAverageInflation()
    {
        await using var context = CreateContext();
        var utcNow = DateTime.UtcNow;
        var inflatedId = Guid.Parse("aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaaaaa");
        var confidentId = Guid.Parse("bbbbbbbb-bbbb-bbbb-bbbb-bbbbbbbbbbbb");

        for (var index = 0; index < 20; index++)
        {
            context.Movies.Add(new Movie
            {
                Id = Guid.Parse($"11111111-1111-1111-1111-{index:D012}"),
                Title = $"Filler {index}",
                VoteAverage = 7.5m,
                VoteCount = 200,
                PosterPath = "/f.jpg",
                CreatedAt = utcNow,
                UpdatedAt = utcNow
            });
        }

        context.Movies.AddRange(
            new Movie
            {
                Id = inflatedId,
                Title = "Inflated Gem",
                VoteAverage = 9.8m,
                VoteCount = 100,
                PosterPath = "/a.jpg",
                CreatedAt = utcNow,
                UpdatedAt = utcNow
            },
            new Movie
            {
                Id = confidentId,
                Title = "Confident Gem",
                VoteAverage = 8.6m,
                VoteCount = 600,
                PosterPath = "/b.jpg",
                CreatedAt = utcNow,
                UpdatedAt = utcNow
            });

        await context.SaveChangesAsync();

        var repository = new SearchRepository(
            context,
            Options.Create(new TopRatedOptions()),
            NullLogger<SearchRepository>.Instance);

        var criteria = new DiscoverBrowseCriteria(
            DiscoverBrowseMode.HiddenGems,
            SearchContentType.Movie,
            [],
            null,
            null,
            null,
            null,
            null,
            null,
            null,
            null,
            null,
            [],
            [],
            null,
            1,
            10);

        var result = await repository.GetHiddenGemsAsync(criteria);

        Assert.True(result.Items.Count >= 2);
        Assert.Equal(confidentId, result.Items[0].Id);
        Assert.NotEqual(inflatedId, result.Items[0].Id);
        Assert.Contains(result.Items, item => item.Id == inflatedId);
    }

    [Fact]
    public async Task GetHiddenGemsAsyncTieBreaksByVoteCountThenTitleThenId()
    {
        await using var context = CreateContext();
        var utcNow = DateTime.UtcNow;
        var firstId = Guid.Parse("cccccccc-cccc-cccc-cccc-cccccccccccc");
        var secondId = Guid.Parse("dddddddd-dddd-dddd-dddd-dddddddddddd");

        context.Movies.AddRange(
            new Movie
            {
                Id = firstId,
                Title = "Alpha Tie",
                VoteAverage = 8.5m,
                VoteCount = 200,
                PosterPath = "/a.jpg",
                CreatedAt = utcNow,
                UpdatedAt = utcNow
            },
            new Movie
            {
                Id = secondId,
                Title = "Beta Tie",
                VoteAverage = 8.5m,
                VoteCount = 200,
                PosterPath = "/b.jpg",
                CreatedAt = utcNow,
                UpdatedAt = utcNow
            });

        await context.SaveChangesAsync();

        var repository = new SearchRepository(
            context,
            Options.Create(new TopRatedOptions()),
            NullLogger<SearchRepository>.Instance);

        var criteria = new DiscoverBrowseCriteria(
            DiscoverBrowseMode.HiddenGems,
            SearchContentType.Movie,
            [],
            null,
            null,
            null,
            null,
            null,
            null,
            null,
            null,
            null,
            [],
            [],
            null,
            1,
            10);

        var result = await repository.GetHiddenGemsAsync(criteria);

        Assert.Equal("Alpha Tie", result.Items[0].Title);
        Assert.Equal("Beta Tie", result.Items[1].Title);
    }

    [Fact]
    public void ComputeWeightedRatingUsesEligiblePopulationFormula()
    {
        const decimal catalogMean = 7.0m;
        const int m = HiddenGemsPolicy.MinimumVoteConfidence;

        var inflated = TopRatedScoreCalculator.ComputeWeightedRating(9.8m, 100, catalogMean, m);
        var confident = TopRatedScoreCalculator.ComputeWeightedRating(8.6m, 600, catalogMean, m);

        Assert.True(confident > inflated);
    }

    [Fact]
    public async Task GetHiddenGemsAsyncExcludesTitlesOutsideVoteBand()
    {
        await using var context = CreateContext();
        var utcNow = DateTime.UtcNow;
        var eligibleId = Guid.Parse("eeeeeeee-eeee-eeee-eeee-eeeeeeeeeeee");

        context.Movies.AddRange(
            new Movie
            {
                Id = Guid.Parse("ffffffff-ffff-ffff-ffff-ffffffffffff"),
                Title = "Too Few Votes",
                VoteAverage = 9.0m,
                VoteCount = 99,
                PosterPath = "/a.jpg",
                CreatedAt = utcNow,
                UpdatedAt = utcNow
            },
            new Movie
            {
                Id = eligibleId,
                Title = "Eligible Gem",
                VoteAverage = 8.0m,
                VoteCount = 150,
                PosterPath = "/b.jpg",
                CreatedAt = utcNow,
                UpdatedAt = utcNow
            });

        await context.SaveChangesAsync();

        var repository = new SearchRepository(
            context,
            Options.Create(new TopRatedOptions()),
            NullLogger<SearchRepository>.Instance);

        var criteria = new DiscoverBrowseCriteria(
            DiscoverBrowseMode.HiddenGems,
            SearchContentType.Movie,
            [],
            null,
            null,
            null,
            null,
            null,
            null,
            null,
            null,
            null,
            [],
            [],
            null,
            1,
            10);

        var result = await repository.GetHiddenGemsAsync(criteria);

        Assert.Single(result.Items);
        Assert.Equal(eligibleId, result.Items[0].Id);
    }

    private static ApplicationDbContext CreateContext()
    {
        var options = new DbContextOptionsBuilder<ApplicationDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;

        return new ApplicationDbContext(options);
    }
}
