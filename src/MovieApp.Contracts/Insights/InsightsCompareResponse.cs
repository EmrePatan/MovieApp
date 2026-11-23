namespace MovieApp.Contracts.Insights;

/// <summary>A genre both users watch, with each user's share of their own history.</summary>
public sealed record InsightsCompareGenreResponse(
    Guid GenreId,
    string Name,
    decimal MyShare,
    decimal TheirShare);

/// <summary>Taste overlap between the signed-in user and another visible profile.</summary>
public sealed record InsightsCompareResponse(
    int MatchPercent,
    IReadOnlyList<InsightsCompareGenreResponse> SharedGenres,
    IReadOnlyList<string> MyTopGenres,
    IReadOnlyList<string> TheirTopGenres,
    int MyWatchMinutes,
    int TheirWatchMinutes);
