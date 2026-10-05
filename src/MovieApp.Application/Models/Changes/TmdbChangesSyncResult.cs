namespace MovieApp.Application.Models.Changes;

public sealed record TmdbChangesSyncResult(
    int WindowsProcessed,
    int ChangedTmdbIdsObserved,
    int RelevantTargets,
    int Refreshed,
    int Skipped,
    int Failed,
    DateOnly? LastCompletedEndDate,
    int ExistingUserRelevantMatches = 0,
    int DiscoveryRelevantMatches = 0,
    int DiscoveryOnlyRelevantMatches = 0);
