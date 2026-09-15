namespace MovieApp.Application.Configuration;

public sealed class TopRatedOptions
{
    public const string SectionName = "TopRated";

    /// <summary>
    /// Minimum vote-confidence threshold (m) for Bayesian weighted rating.
    /// Titles with very few votes are pulled toward the catalog mean.
    /// </summary>
    public int MinimumVoteConfidence { get; set; } = 100;
}
