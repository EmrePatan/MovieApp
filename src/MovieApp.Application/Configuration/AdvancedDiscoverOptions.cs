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

    /// <summary>
    /// TMDB watch-provider ids that are stores (buy/rent), not subscription platforms.
    /// Omitted from streaming discovery chips. Empty list disables the blocklist.
    /// Defaults: 2 Apple TV Store, 3 Google Play Movies, 10 Amazon Video.
    /// </summary>
    public List<int> TransactionalWatchProviderIds { get; set; } = [2, 3, 10];
}
