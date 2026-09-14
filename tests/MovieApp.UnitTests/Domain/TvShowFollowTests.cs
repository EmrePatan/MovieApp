using MovieApp.Domain.Entities;

namespace MovieApp.UnitTests.Domain;

public sealed class TvShowFollowTests
{
    [Fact]
    public void CreateSetsDefaultNotificationPreferences()
    {
        var userId = Guid.NewGuid();
        var tvShowId = Guid.NewGuid();
        var utcNow = DateTime.UtcNow;

        var follow = CatalogFollow.CreateTvFollow(userId, tvShowId, true, true, utcNow);

        Assert.True(follow.NotifyNewSeasons);
        Assert.True(follow.NotifyNewEpisodes);
        Assert.Null(follow.NotifyFromUtc);
        Assert.Null(follow.BaselineEstablishedAtUtc);
        Assert.False(follow.IsBaselineEstablished);
    }

    [Fact]
    public void UpdatePreferencesUpdatesOnlyProvidedValues()
    {
        var follow = CatalogFollow.CreateTvFollow(Guid.NewGuid(), Guid.NewGuid(), true, true, DateTime.UtcNow);
        var updatedAt = DateTime.UtcNow.AddMinutes(1);

        follow.UpdateTvPreferences(false, true, updatedAt);

        Assert.False(follow.NotifyNewSeasons);
        Assert.True(follow.NotifyNewEpisodes);
        Assert.Equal(updatedAt, follow.UpdatedAt);
    }
}
