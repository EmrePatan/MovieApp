namespace MovieApp.Application.Services.Movies;

public static class MovieFollowActionEligibility
{
    public static bool CanFollowForRelease(DateOnly? effectiveReleaseDate, DateOnly today) =>
        !effectiveReleaseDate.HasValue || effectiveReleaseDate.Value > today;

    public static bool CanSetReleaseAlert(DateOnly? effectiveReleaseDate, DateOnly today) =>
        CanFollowForRelease(effectiveReleaseDate, today);
}
