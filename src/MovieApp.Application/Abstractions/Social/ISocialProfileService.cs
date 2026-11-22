using MovieApp.Application.Models.Identity;
using MovieApp.Application.Models.Search;
using MovieApp.Application.Models.Watchlists;

namespace MovieApp.Application.Abstractions.Social;

public sealed record SocialProfileResult(
    Guid Id,
    string UserName,
    string DisplayName,
    DateTime CreatedAt,
    string? AvatarUrl,
    bool IsPrivate,
    string FollowStatus,
    int FollowersCount,
    int FollowingCount,
    string? CoverPosterPath,
    string? CoverImageUrl,
    string? Bio = null,
    ProfileSocialLinksResult? SocialLinks = null);

public sealed record SocialUserSummaryResult(Guid Id, string UserName, string DisplayName, string? AvatarUrl, string FollowStatus);

public sealed record SocialProfileMilestoneResult(string Id, string Title, string Description, DateTime? AchievedAt);

public sealed record SocialProfileShelfItemResult(
    Guid Id,
    string Title,
    string? PosterPath,
    string ContentType,
    int? ProgressPercentage = null);

public sealed record SocialProfileStatisticsResult(
    int MoviesWatched, int EpisodesWatched, int RatingsCount, int ReviewsCount, int FavoritesCount,
    IReadOnlyList<SocialProfileMilestoneResult> Milestones,
    int MovieRuntimeMinutes,
    int EpisodeRuntimeMinutes,
    IReadOnlyList<SocialProfileShelfItemResult> FavoriteMovies,
    IReadOnlyList<SocialProfileShelfItemResult> FavoriteShows,
    IReadOnlyList<SocialProfileShelfItemResult> LibraryTitles,
    IReadOnlyList<SocialProfileShelfItemResult> WatchingTitles,
    IReadOnlyList<SocialProfileShelfItemResult> WatchedTitles,
    IReadOnlyList<SocialProfileShelfItemResult> LikedTitles,
    IReadOnlyList<SocialProfileShelfItemResult> WatchlistTitles);

public interface ISocialProfileService
{
    Task<SocialProfileResult> GetProfileAsync(Guid userId, CancellationToken cancellationToken = default);
    Task<SocialProfileStatisticsResult> GetStatisticsAsync(Guid userId, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<SocialUserSummaryResult>> SearchUsersAsync(string query, int page, int pageSize, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<SocialUserSummaryResult>> GetFollowersAsync(Guid userId, int page, int pageSize, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<SocialUserSummaryResult>> GetFollowingAsync(Guid userId, int page, int pageSize, CancellationToken cancellationToken = default);
    Task SetProfileCoverPosterAsync(string? posterPath, CancellationToken cancellationToken = default);

    /// <summary>
    /// Whether the current viewer may list the target user's written reviews
    /// (profile visibility + ShowReviews privacy).
    /// </summary>
    Task<bool> CanViewReviewsAsync(Guid userId, CancellationToken cancellationToken = default);

    /// <summary>
    /// Watchlists owned by the target user, visible under the same rules as the profile
    /// library (profile visibility + ShowWatched). Returns an empty list when hidden.
    /// </summary>
    Task<IReadOnlyList<WatchlistSummaryResult>> GetWatchlistsAsync(Guid userId, CancellationToken cancellationToken = default);

    /// <summary>
    /// One page of items from a watchlist owned by the target user. Throws NotFoundException
    /// when the list does not exist or is hidden from the viewer.
    /// </summary>
    Task<WatchlistItemsResult> GetWatchlistItemsAsync(
        Guid userId,
        Guid watchlistId,
        SearchContentType mediaType,
        WatchlistItemsSort sort,
        int page,
        int pageSize,
        CancellationToken cancellationToken = default);
}
