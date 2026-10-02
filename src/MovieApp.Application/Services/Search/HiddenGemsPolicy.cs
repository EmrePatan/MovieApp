namespace MovieApp.Application.Services.Search;

/// <summary>
/// Gizli Mücevherler: high rating, modest vote window, poster required.
/// Sort is rating, not release date. See All uses this same local catalog engine.
/// </summary>
public static class HiddenGemsPolicy
{
    public const decimal MinimumVoteAverage = 7.5m;

    public const int MovieMinimumVoteCount = 75;

    public const int MovieMaximumVoteCount = 2000;

    public const int TvMinimumVoteCount = 40;

    public const int TvMaximumVoteCount = 1500;

    public static bool IsMovieHiddenGem(decimal voteAverage, int voteCount, string? posterPath) =>
        voteAverage >= MinimumVoteAverage
        && voteCount >= MovieMinimumVoteCount
        && voteCount <= MovieMaximumVoteCount
        && HasPoster(posterPath);

    public static bool IsTvHiddenGem(decimal voteAverage, int voteCount, string? posterPath) =>
        voteAverage >= MinimumVoteAverage
        && voteCount >= TvMinimumVoteCount
        && voteCount <= TvMaximumVoteCount
        && HasPoster(posterPath);

    public static bool HasPoster(string? posterPath) =>
        !string.IsNullOrWhiteSpace(posterPath);
}
