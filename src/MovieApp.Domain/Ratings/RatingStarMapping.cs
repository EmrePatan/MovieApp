namespace MovieApp.Domain.Ratings;

public static class RatingStarMapping
{
    public const int MinStarBucket = 1;

    public const int MaxStarBucket = 5;

    /// <summary>
    /// Maps a persisted 1-10 <see cref="Entities.Rating.Score"/> to the 1-5 integer
    /// star bucket used by the Reviews histogram filter.
    /// Half-star UI values round up: 3 (1.5★) and 4 (2.0★) both become 2.
    /// Equivalent to <c>(score + 1) / 2</c> integer division and to
    /// <c>Math.ceil(score / 2)</c> on the client.
    /// </summary>
    public static int ToStarBucket(int score)
    {
        RatingScoreRules.Validate(score);
        return (score + 1) / 2;
    }
}
