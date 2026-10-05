using Microsoft.EntityFrameworkCore;
using MovieApp.Application.Common;
using MovieApp.Application.Models.Library;
using MovieApp.Application.Models.Search;
using MovieApp.Domain.Entities;
using MovieApp.Infrastructure.Persistence;
using MovieApp.Infrastructure.Persistence.Repositories;

namespace MovieApp.UnitTests.Library;

public sealed class LibraryRepositoryLikedSearchTests
{
    private static readonly Guid UserId = Guid.Parse("11111111-1111-1111-1111-111111111111");

    [Fact]
    public async Task GetLikedAsync_UnfilteredTotalCountMatchesAllFavorites()
    {
        await using var context = await CreateSeededContextAsync();
        var repository = new LibraryRepository(context);
        var request = CreateRequest(SearchTextMatch.Empty);

        var (_, totalCount) = await repository.GetLikedAsync(
            UserId,
            SearchContentType.Movie,
            request);

        Assert.Equal(5, totalCount);
    }

    private static LibraryPageRequest CreateRequest(SearchTextMatch titleMatch) =>
        new(
            1,
            24,
            25,
            null,
            LibraryCountMode.Required,
            titleMatch,
            titleMatch.IsEmpty ? null : "en-US");

    private static async Task<ApplicationDbContext> CreateSeededContextAsync()
    {
        var context = new ApplicationDbContext(
            new DbContextOptionsBuilder<ApplicationDbContext>()
                .UseInMemoryDatabase($"library-liked-search-{Guid.NewGuid()}")
                .Options);

        var utcNow = DateTime.UtcNow;
        var titles = new[]
        {
            "The First",
            "The Second",
            "The Third",
            "Galaxy Quest",
            "Star Runner",
        };

        foreach (var title in titles)
        {
            var movieId = Guid.NewGuid();
            context.Movies.Add(new Movie
            {
                Id = movieId,
                Title = title,
                VoteAverage = 7,
                VoteCount = 10,
                CreatedAt = utcNow,
                UpdatedAt = utcNow,
            });
            context.Favorites.Add(Favorite.CreateForMovie(UserId, movieId, utcNow));
        }

        await context.SaveChangesAsync();
        return context;
    }
}
