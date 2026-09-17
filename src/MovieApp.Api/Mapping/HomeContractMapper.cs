using MovieApp.Application.Models.Home;
using MovieApp.Contracts.Home;

namespace MovieApp.Api.Mapping;

public static class HomeContractMapper
{
    public static HomeResponse ToHomeResponse(HomeResult result) =>
        new(
            result.Sections.Select(ToHomeSectionResponse).ToList(),
            result.IsPersonalized);

    public static HomeBrowseResponse ToHomeBrowseResponse(HomeBrowseResult result) =>
        new(
            result.Sections.Select(ToHomeSectionResponse).ToList(),
            result.GeneratedAtUtc);

    public static HomePersonalizedResponse ToHomePersonalizedResponse(HomePersonalizedResult result) =>
        new(
            result.Sections.Select(ToHomeSectionResponse).ToList(),
            result.IsPersonalized,
            result.GeneratedAtUtc);

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
            item.VoteCount,
            item.UpcomingKind,
            item.EpisodeId,
            item.SeasonNumber,
            item.EpisodeNumber,
            item.EpisodeName);
}
