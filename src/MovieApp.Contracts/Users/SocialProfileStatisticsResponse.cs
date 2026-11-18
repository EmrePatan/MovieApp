namespace MovieApp.Contracts.Users;

public sealed record SocialProfileMilestoneResponse(
    string Id,
    string Title,
    string Description,
    DateTime? AchievedAt);

public sealed record SocialProfileStatisticsResponse(
    int MoviesWatched,
    int EpisodesWatched,
    int RatingsCount,
    int ReviewsCount,
    int FavoritesCount,
    IReadOnlyList<SocialProfileMilestoneResponse> Milestones);
