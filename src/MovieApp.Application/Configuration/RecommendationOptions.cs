namespace MovieApp.Application.Configuration;

public sealed class RecommendationOptions
{
    public const string SectionName = "Recommendations";

    public int MinimumPersonalizationInteractions { get; set; } = 3;

    public int MaximumCandidates { get; set; } = 500;

    public int HomeSectionItemCount { get; set; } = 10;

    public double SimilarityGenreWeight { get; set; } = 0.50;

    public double SimilarityCastWeight { get; set; } = 0.20;

    public double SimilarityRatingWeight { get; set; } = 0.15;

    public double SimilarityYearWeight { get; set; } = 0.15;

    public double PersonalizedGenreWeight { get; set; } = 0.50;

    public double PersonalizedKeywordWeight { get; set; } = 0.15;

    public double PersonalizedPersonWeight { get; set; }

    public double PersonalizedBehaviorWeight { get; set; } = 0.25;

    public double PersonalizedPopularityWeight { get; set; } = 0.10;

    public double PersonalizedRecencyWeight { get; set; } = 0.05;

    public double FavoriteSignalWeight { get; set; } = 1.0;

    public double StrongRatingSignalWeight { get; set; } = 0.9;

    public double MildRatingSignalWeight { get; set; } = 0.5;

    public int StrongRatingMinScore { get; set; } = 8;

    public int MildRatingMinScore { get; set; } = 6;

    public double WatchedSignalWeight { get; set; } = 0.4;

    public double WatchlistSignalWeight { get; set; } = 0.7;

    public double TvFollowSignalWeight { get; set; } = 0.6;

    public double SearchSignalWeight { get; set; } = 0.2;

    public int RecencyRecentDays { get; set; } = 7;

    public int RecencyMonthDays { get; set; } = 30;

    public double RecencyRecentMultiplier { get; set; } = 1.0;

    public double RecencyMonthMultiplier { get; set; } = 0.8;

    public double RecencyOlderMultiplier { get; set; } = 0.6;

    public int DiversityMaxPerCollection { get; set; } = 2;
}
