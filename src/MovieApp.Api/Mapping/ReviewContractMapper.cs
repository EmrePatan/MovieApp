using MovieApp.Application.Models.Reviews;
using MovieApp.Contracts.Reviews;

namespace MovieApp.Api.Mapping;

public static class ReviewContractMapper
{
    public static ReviewResponse ToResponse(ReviewResult result) =>
        new(
            result.Id,
            new ReviewAuthorResponse(
                result.Author.Id,
                result.Author.DisplayName,
                result.Author.EffectiveAvatarUrl),
            result.Content,
            result.CreatedAt,
            result.UpdatedAt,
            result.UserRating,
            result.AuthoringLocale);

    public static ReviewTranslationResponse ToTranslationResponse(ReviewTranslationResult result) =>
        new(
            result.ReviewId,
            result.Outcome.ToString(),
            result.TranslatedText,
            result.DetectedSourceLanguage,
            result.TargetLocale);

    public static ReviewListResponse ToListResponse(ReviewListPageResult result) =>
        new(
            result.Page.Items.Select(ToResponse).ToList(),
            result.Page.Page,
            result.Page.PageSize,
            result.Page.TotalCount,
            result.Page.TotalPages,
            result.Page.HasNextPage,
            result.Page.HasPreviousPage,
            result.ReviewScoreDistribution);

    public static UserReviewListItemResponse ToUserReviewListItemResponse(UserReviewListItemResult result) =>
        new(
            result.Id,
            result.ContentType,
            result.ContentId,
            result.Title,
            result.PosterPath,
            result.ReleaseDate,
            result.Content,
            result.CreatedAt,
            result.UpdatedAt,
            result.UserRating);

    public static UserReviewListResponse ToUserReviewListResponse(UserReviewListPageResult result) =>
        new(
            result.Page.Items.Select(ToUserReviewListItemResponse).ToList(),
            result.Page.Page,
            result.Page.PageSize,
            result.Page.TotalCount,
            result.Page.TotalPages,
            result.Page.HasNextPage,
            result.Page.HasPreviousPage);
}
