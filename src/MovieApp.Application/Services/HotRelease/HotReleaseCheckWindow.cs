namespace MovieApp.Application.Services.HotRelease;

public static class HotReleaseCheckWindow
{
    public const int DaysBeforeBoundary = 1;

    public const int DaysAfterBoundary = 2;

    public static (DateOnly Start, DateOnly End) ForBoundary(DateOnly boundaryDate) =>
        (boundaryDate.AddDays(-DaysBeforeBoundary), boundaryDate.AddDays(DaysAfterBoundary));
}
