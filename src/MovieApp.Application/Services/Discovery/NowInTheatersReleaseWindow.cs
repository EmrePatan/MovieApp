namespace MovieApp.Application.Services.Discovery;

public static class NowInTheatersReleaseWindow
{
    public static bool Includes(DateOnly? releaseDate, DateOnly today, int maxAgeDays)
    {
        if (releaseDate is null || releaseDate.Value > today)
        {
            return false;
        }

        if (maxAgeDays <= 0)
        {
            return true;
        }

        return releaseDate.Value >= today.AddDays(-maxAgeDays);
    }
}
