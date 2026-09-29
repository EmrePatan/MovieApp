namespace MovieApp.Application.Configuration;

public sealed class AdvancedDiscoverOptions
{
    public const string SectionName = "AdvancedDiscover";

    public const int DefaultMinVoteCountWhenWatchProvider = 200;

    /// <summary>
    /// TMDB <c>vote_count.gte</c> applied only when a watch provider filter is present.
    /// Zero disables the extra floor. Explicit caller minimums below this floor are raised to it.
    /// </summary>
    public int MinVoteCountWhenWatchProvider { get; set; } = DefaultMinVoteCountWhenWatchProvider;
}
