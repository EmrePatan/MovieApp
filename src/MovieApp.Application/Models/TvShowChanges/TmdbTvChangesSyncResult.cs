namespace MovieApp.Application.Models.TvShowChanges;

public sealed record TmdbTvChangesSyncResult(
    int WindowsProcessed,
    int ChangedTmdbIdsObserved,
    int FollowedShowsRefreshed,
    DateOnly? LastCompletedEndDate);
