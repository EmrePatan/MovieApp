using MovieApp.Application.Models.Images;
using MovieApp.Contracts.Images;

namespace MovieApp.Api.Mapping;

public static class ImagesContractMapper
{
    public static ImagesResponse ToResponse(ImagesResult result) =>
        new(
            result.Backdrops.Select(ToImageResponse).ToList(),
            result.Posters.Select(ToImageResponse).ToList(),
            result.Logos.Select(ToImageResponse).ToList(),
            result.Profiles.Select(ToImageResponse).ToList());

    private static ImageResponse ToImageResponse(ImageResult image) =>
        new(
            image.FilePath,
            image.Language,
            image.AspectRatio,
            image.Width,
            image.Height,
            image.VoteAverage,
            image.VoteCount);
}
