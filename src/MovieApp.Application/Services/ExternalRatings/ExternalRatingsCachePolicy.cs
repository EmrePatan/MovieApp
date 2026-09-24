namespace MovieApp.Application.Services.ExternalRatings;

public enum ExternalRatingsCacheState
{
    Miss,
    Fresh,
    StaleUsable,
    Expired,
    NegativeFresh
}

public static class ExternalRatingsCachePolicy
{
    public static ExternalRatingsCacheState Evaluate(
        DateTime? fetchedAtUtc,
        bool isNegative,
        TimeSpan freshWindow,
        TimeSpan staleWindow,
        TimeSpan negativeWindow,
        DateTime utcNow)
    {
        if (fetchedAtUtc is null)
        {
            return ExternalRatingsCacheState.Miss;
        }

        var age = utcNow - fetchedAtUtc.Value;

        if (isNegative)
        {
            return age < negativeWindow
                ? ExternalRatingsCacheState.NegativeFresh
                : ExternalRatingsCacheState.Expired;
        }

        if (age < freshWindow)
        {
            return ExternalRatingsCacheState.Fresh;
        }

        if (age < staleWindow)
        {
            return ExternalRatingsCacheState.StaleUsable;
        }

        return ExternalRatingsCacheState.Expired;
    }
}
