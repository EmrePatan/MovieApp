using MovieApp.Application.Models.Movies;
using MovieApp.Application.Models.Reviews;
using MovieApp.Contracts.Reviews;

namespace MovieApp.Api.Mapping;

public static class ReviewContractMapper
{
    public static ReviewResponse ToResponse(ReviewResult result) =>
        new(
            result.Id,
            new ReviewAuthorResponse(result.Author.Id, result.Author.DisplayName),
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

    public static ReviewListResponse ToListResponse(PaginatedResult<ReviewResult> result) =>
        new(
            result.Items.Select(ToResponse).ToList(),
            result.Page,
            result.PageSize,
            result.TotalCount,
            result.TotalPages,
            result.HasNextPage,
            result.HasPreviousPage);
}
