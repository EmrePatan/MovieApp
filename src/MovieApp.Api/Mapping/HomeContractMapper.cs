using MovieApp.Application.Models.Home;
using MovieApp.Contracts.Home;

namespace MovieApp.Api.Mapping;

public static class HomeContractMapper
{
    public static HomeResponse ToHomeResponse(HomeResult result) =>
        new(
            result.Sections.Select(ToHomeSectionResponse).ToList(),
            result.IsPersonalized);

    private static HomeSectionResponse ToHomeSectionResponse(HomeSection section) =>
        new(
            section.Type.ToString(),
            section.Title,
            section.Items.Select(ToHomeItemResponse).ToList(),
            section.DisplayOrder);

    private static HomeItemResponse ToHomeItemResponse(HomeItem item) =>
        new(
            item.Id,
            item.ContentType,
            item.Title,
            item.OriginalTitle,
            item.PosterUrl,
            item.BackdropUrl,
            item.ReleaseDate,
            item.VoteAverage,
            item.VoteCount);
}
