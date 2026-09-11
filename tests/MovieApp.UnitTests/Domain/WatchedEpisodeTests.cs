using MovieApp.Domain.Entities;

namespace MovieApp.UnitTests.Domain;

public sealed class WatchedEpisodeTests
{
    [Fact]
    public void CreateSetsRequiredFields()
    {
        var userId = Guid.NewGuid();
        var episodeId = Guid.NewGuid();
        var utcNow = DateTime.UtcNow;

        var watchedEpisode = WatchedEpisode.Create(userId, episodeId, utcNow);

        Assert.NotEqual(Guid.Empty, watchedEpisode.Id);
        Assert.Equal(userId, watchedEpisode.UserId);
        Assert.Equal(episodeId, watchedEpisode.EpisodeId);
        Assert.Equal(utcNow, watchedEpisode.WatchedAt);
        Assert.Equal(utcNow, watchedEpisode.CreatedAt);
        Assert.Equal(utcNow, watchedEpisode.UpdatedAt);
    }

    [Fact]
    public void UpdateWatchedAtUpdatesTimestamps()
    {
        var watchedEpisode = WatchedEpisode.Create(Guid.NewGuid(), Guid.NewGuid(), DateTime.UtcNow.AddDays(-1));
        var updatedAt = DateTime.UtcNow;

        watchedEpisode.UpdateWatchedAt(updatedAt);

        Assert.Equal(updatedAt, watchedEpisode.WatchedAt);
        Assert.Equal(updatedAt, watchedEpisode.UpdatedAt);
    }

    [Fact]
    public void CreateThrowsWhenUserIdEmpty()
    {
        Assert.Throws<ArgumentException>(() => WatchedEpisode.Create(Guid.Empty, Guid.NewGuid(), DateTime.UtcNow));
    }

    [Fact]
    public void CreateThrowsWhenEpisodeIdEmpty()
    {
        Assert.Throws<ArgumentException>(() => WatchedEpisode.Create(Guid.NewGuid(), Guid.Empty, DateTime.UtcNow));
    }
}
