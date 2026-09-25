using MovieApp.Infrastructure.Persistence.Repositories;

namespace MovieApp.UnitTests.Recommendations;

public sealed class UserRecommendationContextSeedTests
{
    [Fact]
    public void CreateWatchedTvShowSeedsSkipsShowsMissingFromTheTitleLookup()
    {
        var missingShowId = Guid.NewGuid();
        var presentShowId = Guid.NewGuid();
        var olderWatch = new DateTime(2026, 1, 1, 0, 0, 0, DateTimeKind.Utc);
        var newerWatch = olderWatch.AddDays(2);
        var rows = new List<UserRecommendationContextModels.WatchedEpisodeRow>
        {
            new(missingShowId, newerWatch),
            new(presentShowId, olderWatch),
            new(presentShowId, newerWatch)
        };
        var titles = new Dictionary<Guid, string>
        {
            [presentShowId] = "The Show"
        };

        var seeds = UserRecommendationContextLoader.CreateWatchedTvShowSeeds(rows, titles);

        var seed = Assert.Single(seeds);
        Assert.Equal(presentShowId, seed.ContentId);
        Assert.Equal("tv", seed.ContentType);
        Assert.Equal("The Show", seed.Title);
        Assert.Equal(newerWatch, seed.SignalAtUtc);
    }
}
