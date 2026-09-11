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

    public double PersonalizedGenreWeight { get; set; } = 0.45;

    public double PersonalizedPersonWeight { get; set; } = 0.20;

    public double PersonalizedBehaviorWeight { get; set; } = 0.20;

    public double PersonalizedPopularityWeight { get; set; } = 0.10;

    public double PersonalizedRecencyWeight { get; set; } = 0.05;

    public double FavoriteSignalWeight { get; set; } = 1.0;

    public double WatchedSignalWeight { get; set; } = 0.5;

    public double WatchlistSignalWeight { get; set; } = 0.7;

    public double SearchSignalWeight { get; set; } = 0.2;
}
