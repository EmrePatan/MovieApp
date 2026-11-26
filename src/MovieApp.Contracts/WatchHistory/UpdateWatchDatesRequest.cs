namespace MovieApp.Contracts.WatchHistory;

/// <param name="Mode">day, year, release or unknown.</param>
/// <param name="Date">Required for mode "day".</param>
/// <param name="Year">Required for mode "year".</param>
/// <param name="TvShowId">Every watched episode of this show, instead of individual ids.</param>
/// <param name="Undated">With a show: only its "date unknown" rows (true) or only the dated ones (false).</param>
/// <param name="DryRun">Count what would change without saving anything.</param>
public sealed record UpdateWatchDatesRequest(
    IReadOnlyList<Guid>? MovieIds,
    IReadOnlyList<Guid>? EpisodeIds,
    string? Mode,
    DateOnly? Date,
    int? Year,
    Guid? TvShowId = null,
    bool? Undated = null,
    bool DryRun = false);

public sealed record UpdateWatchDatesResponse(int UpdatedCount, int SkippedCount);
