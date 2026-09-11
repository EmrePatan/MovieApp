using MovieApp.Application.Models.Movies;
using MovieApp.Application.Models.Recommendations;
using MovieApp.Contracts.Recommendations;

namespace MovieApp.Api.Mapping;

public static class RecommendationContractMapper
{
    public static RecommendationResponse ToRecommendationResponse(PaginatedResult<RecommendationItem> result) =>
        new(
            result.Items.Select(ToRecommendationItemResponse).ToList(),
            result.Page,
            result.PageSize,
            result.TotalCount,
            result.TotalPages,
            result.HasNextPage,
            result.HasPreviousPage);

    public static RecommendationHomeResponse ToRecommendationHomeResponse(
        IReadOnlyList<RecommendationSection> sections) =>
        new(sections.Select(ToRecommendationSectionResponse).ToList());

    public static RecommendationSectionResponse ToRecommendationSectionResponse(RecommendationSection section) =>
        new(
            section.Key,
            section.Title,
            section.Items.Select(ToRecommendationItemResponse).ToList());

    public static RecommendationItemResponse ToRecommendationItemResponse(RecommendationItem item) =>
        new(
            item.Id,
            item.Type,
            item.Title,
            item.OriginalTitle,
            item.Overview,
            item.PosterUrl,
            item.BackdropUrl,
            item.ReleaseDate,
            item.VoteAverage,
            item.VoteCount,
            item.Year,
            item.Score,
            item.Reason);
}
