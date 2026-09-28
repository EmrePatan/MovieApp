using Microsoft.EntityFrameworkCore;
using MovieApp.Application.Mapping;
using MovieApp.Application.Models.Search;
using MovieApp.Domain.Entities;
using MovieApp.Infrastructure.Persistence;
using MovieApp.Infrastructure.Persistence.Repositories;

namespace MovieApp.UnitTests.Reviews;

public sealed class ReviewRepositoryUserListTests
{
    [Fact]
    public async Task GetUserReviewsAsync_ProjectsTitleAndKeepsSameTimestampOrderStable()
    {
        await using var context = CreateContext();
        var userId = Guid.NewGuid();
        var olderMovieId = Guid.NewGuid();
        var newerMovieId = Guid.NewGuid();
        var updatedAt = new DateTime(2026, 9, 20, 12, 0, 0, DateTimeKind.Utc);

        context.Movies.AddRange(
            new Movie { Id = olderMovieId, Title = "Older", PosterPath = "/older.jpg", ReleaseDate = new DateOnly(2020, 1, 1) },
            new Movie { Id = newerMovieId, Title = "Newer", PosterPath = "/newer.jpg", ReleaseDate = new DateOnly(2024, 2, 2) });

        var older = Review.CreateForMovie(userId, olderMovieId, "First review", "en-US", updatedAt);
        var newer = Review.CreateForMovie(userId, newerMovieId, "Second review", "en-US", updatedAt);
        older.Id = Guid.Parse("00000000-0000-0000-0000-000000000001");
        newer.Id = Guid.Parse("ffffffff-ffff-ffff-ffff-ffffffffffff");
        older.UpdatedAt = updatedAt;
        newer.UpdatedAt = updatedAt;
        context.Reviews.AddRange(older, newer);
        context.Ratings.Add(Rating.CreateForMovie(userId, newerMovieId, 8, updatedAt));
        await context.SaveChangesAsync();

        var (reviews, totalCount) = await new ReviewRepository(context).GetUserReviewsAsync(
            userId,
            page: 1,
            pageSize: 1,
            SearchContentType.All);

        Assert.Equal(2, totalCount);
        var item = ReviewMapper.ToUserReviewListItemResult(Assert.Single(reviews));
        Assert.Equal(newer.Id, item.Id);
        Assert.Equal("Newer", item.Title);
        Assert.Equal("/newer.jpg", item.PosterPath);
        Assert.Equal(new DateOnly(2024, 2, 2), item.ReleaseDate);
        Assert.Equal(8, item.UserRating);
    }

    private static ApplicationDbContext CreateContext()
    {
        var options = new DbContextOptionsBuilder<ApplicationDbContext>()
            .UseInMemoryDatabase($"user-reviews-{Guid.NewGuid()}")
            .Options;
        return new ApplicationDbContext(options);
    }
}
