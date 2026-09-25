using MovieApp.Application.Models.Identity;
using MovieApp.Application.Services.Localization;
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
            result.CreatedAt,
            result.HasPassword,
            result.LinkedProviders);

    public static UserProfileResponse ToUserProfileResponse(CurrentUserResult result) =>
        new(
            result.Id,
            result.Email,
            result.UserName,
            result.DisplayName,
            result.CreatedAt,
            false,
            []);

    public static UserProfileAuthResponse ToUserProfileAuthResponse(AuthenticationResult result) =>
        new(
            result.AccessToken,
            result.ExpiresAt,
            result.RefreshToken,
            result.RefreshTokenExpiresAt,
            ToUserProfileResponse(result.User));

    public static UserStatisticsResponse ToUserStatisticsResponse(
        UserStatisticsResult result,
        string contentLocale) =>
        new(
            new UserStatisticsSummaryResponse(
                result.Summary.MoviesWatched,
                result.Summary.EpisodesWatched,
                result.Summary.ShowsStarted,
                result.Summary.ShowsCompleted,
                result.Summary.RatingsCount,
                result.Summary.ReviewsCount,
                result.Summary.FavoritesCount,
                result.Summary.WatchlistCount,
                result.Summary.AverageStarRating),
            new UserStatisticsActivityResponse(
                result.Activity.Last12Months
                    .Select(month => new MonthlyActivityResponse(
                        month.Year,
                        month.Month,
                        month.Movies,
                        month.Episodes,
                        month.Total))
                    .ToList(),
                result.Activity.MostActiveMonth is null
                    ? null
                    : new ActivityMonthHighlightResponse(
                        result.Activity.MostActiveMonth.Year,
                        result.Activity.MostActiveMonth.Month,
                        result.Activity.MostActiveMonth.Movies,
                        result.Activity.MostActiveMonth.Episodes,
                        result.Activity.MostActiveMonth.Total),
                result.Activity.CurrentMonthTotal,
                result.Activity.PreviousMonthTotal,
                result.Activity.LongestStreakDays),
            result.Genres
                .Select(genre => new GenreStatisticResponse(genre.GenreId, genre.Name, genre.Count))
                .ToList(),
            new UserStatisticsRatingsResponse(
                result.Ratings.Distribution
                    .Select(item => new StarRatingDistributionResponse(item.Stars, item.Count))
                    .ToList(),
                result.Ratings.MostUsedStars,
                result.Ratings.AverageStarRating),
            new UserStatisticsWatchingMixResponse(
                result.WatchingMix.MovieTitleCount,
                result.WatchingMix.SeriesTitleCount),
            result.Milestones
                .Select(milestone => new ProfileMilestoneResponse(
                    milestone.Id,
                    AchievementMilestoneLocalization.GetTitle(
                        milestone.Id,
                        contentLocale,
                        milestone.Title),
                    AchievementMilestoneLocalization.GetDescription(
                        milestone.Id,
                        contentLocale,
                        milestone.Description),
                    milestone.AchievedAt))
                .ToList(),
            result.Insights.ToList());
}
