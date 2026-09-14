namespace MovieApp.Application.Services.MovieRelease;

public sealed record MovieReleaseCheckResult(
    int MoviesChecked,
    int ReleaseEventsCreated,
    int SkippedProviderFailures,
    int SkippedNotReleased);
