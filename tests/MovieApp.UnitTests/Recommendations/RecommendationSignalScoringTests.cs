using MovieApp.Application.Configuration;
using MovieApp.Application.Models.Recommendations;
using MovieApp.Application.Recommendations;

namespace MovieApp.UnitTests.Recommendations;

public sealed class RecommendationSignalScoringTests
{
    private static readonly RecommendationOptions DefaultOptions = new();
    private static readonly Guid GenreId = Guid.Parse("11111111-1111-1111-1111-111111111111");
    private static readonly DateTime UtcNow = new(2026, 9, 15, 12, 0, 0, DateTimeKind.Utc);

    [Fact]
    public void FavoriteContributionIsStrongerThanWatchlistAndWatched()
    {
        var favorite = CreateSignal(UserBehaviorSignalTypes.Favorite, null, UtcNow.AddDays(-1));
        var watchlist = CreateSignal(UserBehaviorSignalTypes.Watchlist, null, UtcNow.AddDays(-1));
        var watched = CreateSignal(UserBehaviorSignalTypes.Watched, null, UtcNow.AddDays(-1));

        var favoriteContribution = RecommendationSignalScoring.GetSignalContribution(favorite, DefaultOptions, UtcNow);
        var watchlistContribution = RecommendationSignalScoring.GetSignalContribution(watchlist, DefaultOptions, UtcNow);
        var watchedContribution = RecommendationSignalScoring.GetSignalContribution(watched, DefaultOptions, UtcNow);

        Assert.True(favoriteContribution > watchlistContribution);
        Assert.True(watchlistContribution > watchedContribution);
    }

    [Fact]
    public void StrongRatingContributionIsStrongerThanMildRating()
    {
        var strong = CreateSignal(UserBehaviorSignalTypes.Rating, 9, UtcNow.AddDays(-1));
        var mild = CreateSignal(UserBehaviorSignalTypes.Rating, 6, UtcNow.AddDays(-1));

        var strongContribution = RecommendationSignalScoring.GetSignalContribution(strong, DefaultOptions, UtcNow);
        var mildContribution = RecommendationSignalScoring.GetSignalContribution(mild, DefaultOptions, UtcNow);

        Assert.True(strongContribution > mildContribution);
        Assert.True(strongContribution > 0m);
        Assert.True(mildContribution > 0m);
    }

    [Fact]
    public void LowRatingHasNoPositiveContribution()
    {
        var lowRating = CreateSignal(UserBehaviorSignalTypes.Rating, 4, UtcNow.AddDays(-1));

        Assert.Equal(0m, RecommendationSignalScoring.GetSignalContribution(lowRating, DefaultOptions, UtcNow));
    }

    [Fact]
    public void RecencyMultiplierAppliesRecentMonthAndOlderBuckets()
    {
        var recent = CreateSignal(UserBehaviorSignalTypes.Favorite, null, UtcNow.AddDays(-3));
        var monthOld = CreateSignal(UserBehaviorSignalTypes.Favorite, null, UtcNow.AddDays(-20));
        var older = CreateSignal(UserBehaviorSignalTypes.Favorite, null, UtcNow.AddDays(-60));

        var recentContribution = RecommendationSignalScoring.GetSignalContribution(recent, DefaultOptions, UtcNow);
        var monthContribution = RecommendationSignalScoring.GetSignalContribution(monthOld, DefaultOptions, UtcNow);
        var olderContribution = RecommendationSignalScoring.GetSignalContribution(older, DefaultOptions, UtcNow);

        Assert.True(recentContribution > monthContribution);
        Assert.True(monthContribution > olderContribution);
    }

    [Fact]
    public void TvFollowHasMediumPositiveContribution()
    {
        var tvFollow = CreateSignal(UserBehaviorSignalTypes.TvFollow, null, UtcNow.AddDays(-2));
        var watched = CreateSignal(UserBehaviorSignalTypes.Watched, null, UtcNow.AddDays(-2));

        var tvFollowContribution = RecommendationSignalScoring.GetSignalContribution(tvFollow, DefaultOptions, UtcNow);
        var watchedContribution = RecommendationSignalScoring.GetSignalContribution(watched, DefaultOptions, UtcNow);

        Assert.True(tvFollowContribution > watchedContribution);
    }

    [Theory]
    [InlineData(UserBehaviorSignalTypes.Rating)]
    [InlineData(UserBehaviorSignalTypes.Favorite)]
    [InlineData(UserBehaviorSignalTypes.Watched)]
    [InlineData(UserBehaviorSignalTypes.Watchlist)]
    [InlineData(UserBehaviorSignalTypes.TvFollow)]
    public void MeaningfulInteractionSignalsAreRecognized(string signalType)
    {
        Assert.True(RecommendationSignalScoring.IsMeaningfulInteractionSignal(signalType));
    }

    [Fact]
    public void SearchIsNotMeaningfulInteractionSignal()
    {
        Assert.False(RecommendationSignalScoring.IsMeaningfulInteractionSignal(UserBehaviorSignalTypes.Search));
    }

    private static UserBehaviorSignal CreateSignal(
        string signalType,
        int? rating,
        DateTime? signalAtUtc) =>
        new(
            Guid.NewGuid(),
            "movie",
            signalType,
            "Title",
            rating,
            signalAtUtc,
            [GenreId],
            new Dictionary<Guid, string> { [GenreId] = "Science Fiction" },
            []);
}
