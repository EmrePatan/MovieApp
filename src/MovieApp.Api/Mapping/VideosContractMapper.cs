using MovieApp.Application.Models.Videos;
using MovieApp.Contracts.Videos;

namespace MovieApp.Api.Mapping;

public static class VideosContractMapper
{
    public static VideosResponse ToResponse(VideosResult result) =>
        new(result.Primary is null ? null : ToPrimaryVideoResponse(result.Primary));

    private static PrimaryVideoResponse ToPrimaryVideoResponse(PrimaryVideoResult primary) =>
        new(
            primary.Site,
            primary.Type,
            primary.Name,
            primary.Language,
            primary.Official,
            primary.WatchUrl);
}
