namespace MovieApp.Application.Models.HotRelease;

public sealed record HotReleaseCheckResult(
    int Candidates,
    int Checked,
    int Hydrated,
    int ReleaseEventsCreated,
    int Failures);
