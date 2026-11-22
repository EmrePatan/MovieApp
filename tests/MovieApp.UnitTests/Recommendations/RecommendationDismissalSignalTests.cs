using MovieApp.Application.Configuration;
using MovieApp.Application.Models.Recommendations;
using MovieApp.Application.Recommendations;
using MovieApp.Infrastructure.Persistence.Repositories;

namespace MovieApp.UnitTests.Recommendations;

public sealed class RecommendationDismissalSignalTests
{
    private static readonly RecommendationOptions DefaultOptions = new();
    private static readonly Guid GenreId = Guid.Parse("22222222-2222-2222-2222-222222222222");
    private static readonly DateTime UtcNow = new(2026, 11, 22, 12, 0, 0, DateTimeKind.Utc);

    [Fact]
    public void DismissedContributionIsNegative()
    {
        var dismissed = CreateSignal(UserBehaviorSignalTypes.Dismissed, null, UtcNow.AddDays(-1));

        var contribution = RecommendationSignalScoring.GetSignalContribution(dismissed, DefaultOptions, UtcNow);

        Assert.True(contribution < 0m);
    }

    [Fact]
    public void DismissedPushesTasteDownHarderThanLowRating()
    {
        var dismissed = CreateSignal(UserBehaviorSignalTypes.Dismissed, null, UtcNow.AddDays(-1));
        var lowRating = CreateSignal(UserBehaviorSignalTypes.Rating, 2, UtcNow.AddDays(-1));

        var dismissedContribution = RecommendationSignalScoring.GetSignalContribution(dismissed, DefaultOptions, UtcNow);
        var lowRatingContribution = RecommendationSignalScoring.GetSignalContribution(lowRating, DefaultOptions, UtcNow);

        Assert.True(dismissedContribution < lowRatingContribution);
    }

    [Fact]
    public void PositiveDismissWeightConfigurationIsTreatedAsNegativeMagnitude()
    {
        var options = new RecommendationOptions { DismissSignalWeight = 0.8 };
        var dismissed = CreateSignal(UserBehaviorSignalTypes.Dismissed, null, UtcNow.AddDays(-1));

        var contribution = RecommendationSignalScoring.GetSignalContribution(dismissed, options, UtcNow);

        Assert.True(contribution < 0m);
    }

    [Fact]
    public void DismissedIsNeitherPositiveTasteNorMeaningfulInteraction()
    {
        var dismissed = CreateSignal(UserBehaviorSignalTypes.Dismissed, null, UtcNow.AddDays(-1));

        Assert.False(RecommendationSignalScoring.IsMeaningfulInteractionSignal(UserBehaviorSignalTypes.Dismissed));
        Assert.False(RecommendationSignalScoring.IsPositiveTasteSignal(dismissed, DefaultOptions, UtcNow));
    }

    [Fact]
    public void CollapseSeedsPrefersDismissalOverFavoriteAndLowRatingForTheSameTitle()
    {
        var movieId = Guid.NewGuid();
        var seeds = new List<UserRecommendationContextModels.SignalSeed>
        {
            new(
                movieId,
                "movie",
                UserBehaviorSignalTypes.Favorite,
                "Avengers",
                null,
                new DateTime(2026, 11, 1, 0, 0, 0, DateTimeKind.Utc)),
            new(
                movieId,
                "movie",
                UserBehaviorSignalTypes.Rating,
                "Avengers",
                2,
                new DateTime(2026, 11, 2, 0, 0, 0, DateTimeKind.Utc)),
            new(
                movieId,
                "movie",
                UserBehaviorSignalTypes.Dismissed,
                "Avengers",
                null,
                new DateTime(2026, 11, 3, 0, 0, 0, DateTimeKind.Utc))
        };

        var collapsed = UserRecommendationContextLoader.CollapseSeeds(seeds, mildRatingMinScore: 6);

        var seed = Assert.Single(collapsed);
        Assert.Equal(UserBehaviorSignalTypes.Dismissed, seed.SignalType);
    }

    private static UserBehaviorSignal CreateSignal(string signalType, int? rating, DateTime? signalAtUtc) =>
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
