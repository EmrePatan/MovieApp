namespace MovieApp.Application.Models.Recommendations;

public sealed record UserRecommendationContext(
    IReadOnlyList<UserBehaviorSignal> Signals,
    IReadOnlySet<Guid> ExcludedMovieIds,
    IReadOnlySet<Guid> ExcludedTvShowIds,
    int MeaningfulInteractionCount);
