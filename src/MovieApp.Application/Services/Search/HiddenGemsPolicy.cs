namespace MovieApp.Application.Services.Search;

/// <summary>
/// Gizli Mücevherler: high rating, modest vote window, poster required.
/// Sort is rating, not release date. See All uses this same local catalog engine.
/// </summary>
public static class HiddenGemsPolicy
{
    public const decimal MinimumVoteAverage = 7.5m;

    public const int MinimumVoteConfidence = 300;

    public const int MovieMinimumVoteCount = 100;

    public const int MovieMaximumVoteCount = 800;

    public const int TvMinimumVoteCount = 50;

    public const int TvMaximumVoteCount = 850;

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
