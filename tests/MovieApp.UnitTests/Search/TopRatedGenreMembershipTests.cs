using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using MovieApp.Application.Configuration;
using MovieApp.Application.Models.Search;
using MovieApp.Domain.Entities;
using MovieApp.Infrastructure.Persistence;
using MovieApp.Infrastructure.Persistence.Repositories;

namespace MovieApp.UnitTests.Search;

public sealed class TopRatedGenreMembershipTests
{
    [Fact]
    public async Task GetContentKeysWithGenreAsyncReturnsCatalogGenreMatches()
    {
        await using var context = CreateContext();
        var utcNow = DateTime.UtcNow;
        var animationGenreId = Guid.Parse("aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaaaaa");
        var dramaGenreId = Guid.Parse("bbbbbbbb-bbbb-bbbb-bbbb-bbbbbbbbbbbb");
        var animatedMovieId = Guid.Parse("cccccccc-cccc-cccc-cccc-cccccccccccc");
        var dramaMovieId = Guid.Parse("dddddddd-dddd-dddd-dddd-dddddddddddd");

        context.Genres.AddRange(
            new Genre
            {
                Id = animationGenreId,
                Name = "Animation",
                CreatedAt = utcNow
            },
            new Genre
            {
                Id = dramaGenreId,
                Name = "Drama",
                CreatedAt = utcNow
            });

        context.Movies.AddRange(
            new Movie
            {
                Id = animatedMovieId,
                Title = "Animated Film",
                VoteAverage = 8m,
                VoteCount = 100,
                CreatedAt = utcNow,
                UpdatedAt = utcNow
            },
            new Movie
            {
                Id = dramaMovieId,
                Title = "Drama Film",
                VoteAverage = 8m,
                VoteCount = 100,
                CreatedAt = utcNow,
                UpdatedAt = utcNow
            });

        context.MovieGenres.AddRange(
            new MovieGenre { MovieId = animatedMovieId, GenreId = animationGenreId },
            new MovieGenre { MovieId = dramaMovieId, GenreId = dramaGenreId });
        await context.SaveChangesAsync();

        var repository = new SearchRepository(context, Options.Create(new TopRatedOptions()));
        var items = new List<SearchItem>
        {
            new(animatedMovieId, "movie", "Animated Film", null, null, null, null, null, 8m, 100, null),
            new(dramaMovieId, "movie", "Drama Film", null, null, null, null, null, 8m, 100, null)
        };

        var keys = await repository.GetContentKeysWithGenreAsync(items, animationGenreId);

        Assert.Single(keys);
        Assert.Contains(new CatalogContentKey(animatedMovieId, "movie"), keys);
    }

    private static ApplicationDbContext CreateContext()
    {
        var options = new DbContextOptionsBuilder<ApplicationDbContext>()
            .UseInMemoryDatabase($"top-rated-genre-membership-{Guid.NewGuid()}")
            .Options;

        return new ApplicationDbContext(options);
    }
}
