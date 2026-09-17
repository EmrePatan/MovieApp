using MovieApp.Application.Mapping;
using MovieApp.Domain.Entities;

namespace MovieApp.UnitTests.Watchlists;

public sealed class WatchlistMapperTests
{
    [Fact]
    public void ToItemsResult_preserves_server_order_in_items()
    {
        var movie = new Movie
        {
            Id = Guid.NewGuid(),
            Title = "Zulu",
            OriginalTitle = "Zulu",
            PosterPath = null,
            BackdropPath = null,
            ReleaseDate = new DateOnly(2020, 1, 1),
            VoteAverage = 7.5m,
        };

        var tvShow = new TvShow
        {
            Id = Guid.NewGuid(),
            Title = "Alpha Show",
            OriginalTitle = "Alpha Show",
            PosterPath = null,
            BackdropPath = null,
            FirstAirDate = new DateOnly(2019, 1, 1),
            VoteAverage = 8.1m,
        };

        var items = new List<WatchlistItem>
        {
            new()
            {
                Id = Guid.NewGuid(),
                WatchlistId = Guid.NewGuid(),
                MovieId = movie.Id,
                Movie = movie,
                CreatedAt = DateTime.UtcNow.AddDays(-2),
            },
            new()
            {
                Id = Guid.NewGuid(),
                WatchlistId = Guid.NewGuid(),
                TvShowId = tvShow.Id,
                TvShow = tvShow,
                CreatedAt = DateTime.UtcNow.AddDays(-1),
            },
        };

        var result = WatchlistMapper.ToItemsResult(items, 1, 20, 2);

        Assert.Equal(["movie", "tv"], result.Items.Select(item => item.ContentType).ToArray());
        Assert.Equal(["Zulu", "Alpha Show"], result.Items.Select(item => item.Title).ToArray());
    }
}
