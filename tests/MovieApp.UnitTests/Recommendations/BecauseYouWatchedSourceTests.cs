using MovieApp.Application.Models.Recommendations;
using MovieApp.Application.Recommendations;
using MovieApp.Application.Services.Recommendations;

namespace MovieApp.UnitTests.Recommendations;

public sealed class BecauseYouWatchedSourceTests
{
    [Fact]
    public void SelectBecauseYouWatchedSources_PrefersRecentWatchesAcrossMoviesAndTv()
    {
        var recentMovie = Guid.Parse("aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaaaaa");
        var olderMovie = Guid.Parse("bbbbbbbb-bbbb-bbbb-bbbb-bbbbbbbbbbbb");
        var oldestMovie = Guid.Parse("cccccccc-cccc-cccc-cccc-cccccccccccc");
        var recentShow = Guid.Parse("dddddddd-dddd-dddd-dddd-dddddddddddd");
        var now = new DateTime(2026, 9, 25, 12, 0, 0, DateTimeKind.Utc);

        var signals = new[]
        {
            Watched(oldestMovie, "movie", now.AddDays(-10)),
            Watched(olderMovie, "movie", now.AddDays(-2)),
            Watched(recentMovie, "movie", now.AddDays(-1)),
            Watched(recentShow, "tv", now),
            new UserBehaviorSignal(
                Guid.NewGuid(),
                "movie",
                UserBehaviorSignalTypes.Favorite,
                "Favorite",
                null,
                now.AddMinutes(1),
                [],
                new Dictionary<Guid, string>(),
                [])
        };

        var sources = RecommendationService.SelectBecauseYouWatchedSources(signals);

        Assert.Equal([recentShow, recentMovie, olderMovie], sources.Select(signal => signal.ContentId).ToList());
    }

    private static UserBehaviorSignal Watched(Guid contentId, string contentType, DateTime watchedAt) =>
        new(
            contentId,
            contentType,
            UserBehaviorSignalTypes.Watched,
            "Title",
            null,
            watchedAt,
            [],
            new Dictionary<Guid, string>(),
            []);
}
