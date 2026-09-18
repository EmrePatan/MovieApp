namespace MovieApp.Application.Models.AiRecommendations;

public static class AiRecommendationQuotaMessages
{
    public const string DailyLimitTitle = "Daily limit reached";

    public static string DailyUserLimitReached(int dailyLimit) =>
        $"You've used all {dailyLimit} AI recommendation requests for today. Try again tomorrow.";
}
