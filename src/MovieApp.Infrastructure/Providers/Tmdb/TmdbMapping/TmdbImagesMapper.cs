using MovieApp.Application.Models.Images;
using MovieApp.Infrastructure.Providers.Tmdb.TmdbModels;

namespace MovieApp.Infrastructure.Providers.Tmdb.TmdbMapping;

internal static class TmdbImagesMapper
{
    public static ProviderImagesResult ToProviderImagesResult(TmdbImagesResponseJson response) =>
        new(
            MapImages(response.Backdrops),
            MapImages(response.Posters),
            MapImages(response.Logos),
            MapImages(response.Profiles));

    private static List<ProviderImageResult> MapImages(IReadOnlyList<TmdbImageJson> images) =>
        images
            .Select(MapImage)
            .Where(image => image is not null)
            .Cast<ProviderImageResult>()
            .ToList();

    private static ProviderImageResult? MapImage(TmdbImageJson image)
    {
        var filePath = TmdbMovieMapper.NormalizeImagePath(image.FilePath);
        if (filePath is null)
        {
            return null;
        }

        return new ProviderImageResult(
            filePath,
            string.IsNullOrWhiteSpace(image.Iso6391) ? null : image.Iso6391.Trim().ToLowerInvariant(),
            Convert.ToDecimal(image.AspectRatio),
            image.Width,
            image.Height,
            Convert.ToDecimal(image.VoteAverage),
            image.VoteCount);
    }
}
