namespace MovieApp.Domain.Ratings;

public static class RatingScoreRules
{
    public const int MinScore = 1;

    public const int MaxScore = 10;

    public static void Validate(int score)
    {
        if (score < MinScore || score > MaxScore)
        {
            throw new ArgumentOutOfRangeException(
                nameof(score),
                score,
                $"Score must be between {MinScore} and {MaxScore}.");
        }
    }
}
