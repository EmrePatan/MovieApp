namespace MovieApp.Application.Configuration;

public sealed class RecommendationOptions
{
    public const string SectionName = "Recommendations";

    public int MinimumPersonalizationInteractions { get; set; } = 3;

    public int MaximumCandidates { get; set; } = 500;

    /// <summary>
    /// Candidate budget for the Home "Because You Watched" rail. The full recommendation
    /// endpoint keeps the larger MaximumCandidates budget; Home only needs a bounded pool
    /// before taking the visible section size.
    /// </summary>
    public int HomeBecauseYouWatchedMaximumCandidates { get; set; } = 80;

    public int HomeSectionItemCount { get; set; } = 10;

    /// <summary>
    /// Extra personalized items returned for the home rail so hero dedup can drop overlaps and still fill the section.
    /// </summary>
    public int HomeRecommendationSurplus { get; set; } = 10;

    /// <summary>
    /// Minimum slots reserved for movies and for TV when content type is all.
    /// The split is half/half, and this floor is clamped so it never exceeds half the budget.
    /// </summary>
    public int CandidateMinorityTypeMinimum { get; set; } = 100;

    /// <summary>
    /// Titles below this vote count are left out of the personalized pool. Zero disables the floor.
    /// </summary>
    public int CandidateMinVoteCount { get; set; } = 20;

    /// <summary>
    /// When several genres are preferred, each genre keeps at least this many vote-leaders before the global fill.
    /// </summary>
    public int CandidateMinPerGenre { get; set; } = 8;

    public double SimilarityGenreWeight { get; set; } = 0.50;

    public double SimilarityCastWeight { get; set; } = 0.20;

    public double SimilarityRatingWeight { get; set; } = 0.15;

    public double SimilarityYearWeight { get; set; } = 0.15;

    public double PersonalizedGenreWeight { get; set; } = 0.50;

    public double PersonalizedKeywordWeight { get; set; } = 0.15;

    /// <summary>
    /// Not applied. Personalized movie hydration does not load cast, and behavior similarity
    /// scores empty person lists, so wiring this would only affect TV and would disagree with
    /// <c>ScoreCandidatesDoesNotUsePersonOverlap</c>.
    /// </summary>
    public double PersonalizedPersonWeight { get; set; }

    public double PersonalizedBehaviorWeight { get; set; } = 0.25;

    public double PersonalizedPopularityWeight { get; set; } = 0.10;

    public double PersonalizedRecencyWeight { get; set; } = 0.05;

    public double FavoriteSignalWeight { get; set; } = 1.0;

    public double StrongRatingSignalWeight { get; set; } = 0.9;

    public double MildRatingSignalWeight { get; set; } = 0.5;

    public int StrongRatingMinScore { get; set; } = 8;

    public int MildRatingMinScore { get; set; } = 6;

    /// <summary>
    /// Taste push for a rating below <see cref="MildRatingMinScore"/>. Negative values push genres and keywords down.
    /// A positive configuration is treated as a negative magnitude.
    /// </summary>
    public double LowRatingSignalWeight { get; set; } = -0.6;

    public double WatchedSignalWeight { get; set; } = 0.4;

    public double WatchlistSignalWeight { get; set; } = 0.7;

    public double TvFollowSignalWeight { get; set; } = 0.6;

    public double SearchSignalWeight { get; set; } = 0.2;

    public int RecencyRecentDays { get; set; } = 7;

    public int RecencyMonthDays { get; set; } = 30;

    public double RecencyRecentMultiplier { get; set; } = 1.0;

    public double RecencyMonthMultiplier { get; set; } = 0.8;

    public double RecencyOlderMultiplier { get; set; } = 0.6;

    public int DiversityMaxPerCollection { get; set; } = 1;

    public int DiversityMaxPerGenre { get; set; } = 4;

    /// <summary>
    /// Hard cap for titles that share a configured franchise-family keyword. Zero disables the cap.
    /// </summary>
    public int DiversityMaxPerFranchiseFamily { get; set; } = 2;

    /// <summary>
    /// Exact catalog keyword names (case-insensitive) that mark a franchise family beyond a single TMDB collection.
    /// Production companies are not stored. Unmatched names do nothing.
    /// </summary>
    public List<string> DiversityFranchiseKeywordNames { get; set; } =
    [
        "marvel cinematic universe",
        "dc extended universe",
        "star wars",
        "james bond"
    ];
}
