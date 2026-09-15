using MovieApp.Application.Configuration;
using MovieApp.Application.Models.Recommendations;

namespace MovieApp.Application.Recommendations;

public static class RecommendationSignalScoring
{
    public static decimal GetSignalContribution(
        UserBehaviorSignal signal,
        RecommendationOptions options,
        DateTime utcNow)
    {
        var baseWeight = GetBaseSignalWeight(signal, options);
        if (baseWeight <= 0m)
        {
            return 0m;
        }

        if (signal.SignalAtUtc is null)
        {
            return baseWeight;
        }

        return baseWeight * GetRecencyMultiplier(signal.SignalAtUtc.Value, utcNow, options);
    }

    public static decimal GetBaseSignalWeight(UserBehaviorSignal signal, RecommendationOptions options)
    {
        return signal.SignalType switch
        {
            UserBehaviorSignalTypes.Rating when signal.RatingScore is not null =>
                GetRatingSignalWeight(signal.RatingScore.Value, options),
            UserBehaviorSignalTypes.Favorite => (decimal)options.FavoriteSignalWeight,
            UserBehaviorSignalTypes.Watchlist => (decimal)options.WatchlistSignalWeight,
            UserBehaviorSignalTypes.TvFollow => (decimal)options.TvFollowSignalWeight,
            UserBehaviorSignalTypes.Watched => (decimal)options.WatchedSignalWeight,
            UserBehaviorSignalTypes.Search => (decimal)options.SearchSignalWeight,
            _ => 0m
        };
    }

    public static decimal GetRatingSignalWeight(int ratingScore, RecommendationOptions options)
    {
        if (ratingScore >= options.StrongRatingMinScore)
        {
            return (decimal)options.StrongRatingSignalWeight;
        }

        if (ratingScore >= options.MildRatingMinScore)
        {
            return (decimal)options.MildRatingSignalWeight;
        }

        return 0m;
    }

    public static decimal GetRecencyMultiplier(
        DateTime signalAtUtc,
        DateTime utcNow,
        RecommendationOptions options)
    {
        var age = utcNow - signalAtUtc;
        if (age.TotalDays <= options.RecencyRecentDays)
        {
            return (decimal)options.RecencyRecentMultiplier;
        }

        if (age.TotalDays <= options.RecencyMonthDays)
        {
            return (decimal)options.RecencyMonthMultiplier;
        }

        return (decimal)options.RecencyOlderMultiplier;
    }

    public static int GetSignalPriority(string signalType) =>
        signalType switch
        {
            UserBehaviorSignalTypes.Favorite => 6,
            UserBehaviorSignalTypes.Rating => 5,
            UserBehaviorSignalTypes.Watchlist => 4,
            UserBehaviorSignalTypes.TvFollow => 3,
            UserBehaviorSignalTypes.Watched => 2,
            UserBehaviorSignalTypes.Search => 1,
            _ => 0
        };

    public static bool IsMeaningfulInteractionSignal(string signalType) =>
        signalType is UserBehaviorSignalTypes.Rating
            or UserBehaviorSignalTypes.Favorite
            or UserBehaviorSignalTypes.Watched
            or UserBehaviorSignalTypes.Watchlist
            or UserBehaviorSignalTypes.TvFollow;
}
