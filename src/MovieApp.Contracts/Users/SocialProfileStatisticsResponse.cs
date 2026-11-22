namespace MovieApp.Contracts.Users;

public sealed record SocialProfileMilestoneResponse(
    string Id,
    string Title,
    string Description,
    DateTime? AchievedAt);

public sealed record SocialProfileShelfItemResponse(
    Guid Id,
    string Title,
    string? PosterPath,
    string ContentType,
    int? ProgressPercentage = null);

public sealed record SocialProfileStatisticsResponse(
    int MoviesWatched,
    int EpisodesWatched,
    int RatingsCount,
    int ReviewsCount,
    int FavoritesCount,
    IReadOnlyList<SocialProfileMilestoneResponse> Milestones,
    int MovieRuntimeMinutes,
    int EpisodeRuntimeMinutes,
    IReadOnlyList<SocialProfileShelfItemResponse> FavoriteMovies,
    IReadOnlyList<SocialProfileShelfItemResponse> FavoriteShows,
    IReadOnlyList<SocialProfileShelfItemResponse> LibraryTitles,
    IReadOnlyList<SocialProfileShelfItemResponse> WatchingTitles,
    IReadOnlyList<SocialProfileShelfItemResponse> WatchedTitles,
    IReadOnlyList<SocialProfileShelfItemResponse> LikedTitles,
    IReadOnlyList<SocialProfileShelfItemResponse> WatchlistTitles);
