namespace MovieApp.Application.Models.Changes;

public enum TmdbChangesTargetRefreshOutcome
{
    Refreshed = 0,
    SkippedUnavailable = 1,
    SkippedNotFound = 2,
    Failed = 3
}
