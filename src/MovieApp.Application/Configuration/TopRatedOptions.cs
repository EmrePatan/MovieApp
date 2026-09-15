namespace MovieApp.Application.Configuration;

public sealed class TopRatedOptions
{
    public const string SectionName = "TopRated";

    /// <summary>
    /// Minimum vote-confidence threshold (m) for Bayesian weighted rating.
    /// Titles with very few votes are pulled toward the catalog mean.
    /// </summary>
    public int MinimumVoteConfidence { get; set; } = 100;

    /// <summary>
    /// Catalog genre name used for Home Top Rated animation diversity (TMDB genre id 16).
    /// </summary>
    public string HomeRailAnimationGenreName { get; set; } = "Animation";

    /// <summary>
    /// Maximum Animation titles allowed on the Home Top Rated rail.
    /// </summary>
    public int HomeRailMaxAnimationItems { get; set; } = 3;

    /// <summary>
    /// Number of Bayesian-ranked candidates to fetch before applying Home diversity caps.
    /// </summary>
    public int HomeRailCandidateFetchSize { get; set; } = 50;
}
