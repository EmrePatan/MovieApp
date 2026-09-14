using MovieApp.Domain.Entities;
using MovieApp.Domain.Enums;

namespace MovieApp.UnitTests.Domain;

public sealed class CatalogFollowTests
{
    [Fact]
    public void CreateTvFollowSetsTvDefaults()
    {
        var follow = CatalogFollow.CreateTvFollow(Guid.NewGuid(), Guid.NewGuid(), true, true, DateTime.UtcNow);

        Assert.Equal(CatalogContentType.Tv, follow.ContentType);
        Assert.False(follow.NotifyMovieRelease);
        Assert.True(follow.NotifyNewSeasons);
        Assert.True(follow.NotifyNewEpisodes);
    }

    [Fact]
    public void CreateMovieFollowSetsMovieDefaults()
    {
        var follow = CatalogFollow.CreateMovieFollow(Guid.NewGuid(), Guid.NewGuid(), DateTime.UtcNow);

        Assert.Equal(CatalogContentType.Movie, follow.ContentType);
        Assert.True(follow.NotifyMovieRelease);
        Assert.False(follow.NotifyNewSeasons);
        Assert.False(follow.NotifyNewEpisodes);
    }

    [Fact]
    public void MovieReleasedDedupeKey_IsStable()
    {
        var movieId = Guid.Parse("aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaaaaa");

        Assert.Equal(
            "movie:aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaaaaa:released",
            MovieApp.Domain.Notifications.CatalogReleaseEventDedupeKey.ForMovieReleased(movieId));
    }
}
