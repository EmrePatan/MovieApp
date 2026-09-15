namespace MovieApp.Application.Services.Movies;

public static class MovieConsumptionReleaseGuardrail
{
    public static bool IsReleasedForConsumption(DateOnly? effectiveReleaseDate, DateOnly today)
    {
        if (!effectiveReleaseDate.HasValue)
        {
            return true;
        }

        return effectiveReleaseDate.Value <= today;
    }
}
