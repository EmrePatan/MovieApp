using MovieApp.Application.Models.Identity;
using MovieApp.Contracts.Users;

namespace MovieApp.Api.Mapping;

public static class UserProfileContractMapper
{
    public static UserProfileResponse ToUserProfileResponse(UserProfileResult result) =>
        new(
            result.Id,
            result.Email,
            result.UserName,
            result.DisplayName,
            result.CreatedAt);

    public static UserProfileResponse ToUserProfileResponse(CurrentUserResult result) =>
        new(
            result.Id,
            result.Email,
            result.UserName,
            result.DisplayName,
            result.CreatedAt);

    public static UserProfileAuthResponse ToUserProfileAuthResponse(AuthenticationResult result) =>
        new(
            result.AccessToken,
            result.ExpiresAt,
            ToUserProfileResponse(result.User));

    public static UserStatisticsResponse ToUserStatisticsResponse(UserStatisticsResult result) =>
        new(
            result.FavoriteMovieCount,
            result.FavoriteTvShowCount,
            result.WatchlistCount,
            result.WatchlistItemCount,
            result.RatedMovieCount,
            result.RatedTvShowCount,
            result.ReviewedMovieCount,
            result.ReviewedTvShowCount,
            result.WatchedMovieCount,
            result.WatchedEpisodeCount,
            result.TotalRatingCount,
            result.TotalReviewCount,
            result.TotalWatchedCount);
}
