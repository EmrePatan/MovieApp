using MovieApp.Application.Models.Images;

namespace MovieApp.Application.Services.Images;

public static class ImageGalleryOrderer
{
    public static ImagesResult ToImagesResult(ProviderImagesResult provider, string? preferredLanguage) =>
        new(
            Order(provider.Backdrops, preferredLanguage),
            Order(provider.Posters, preferredLanguage),
            Order(provider.Logos, preferredLanguage),
            Order(provider.Profiles, preferredLanguage));

    internal static IReadOnlyList<ImageResult> Order(
        IReadOnlyList<ProviderImageResult> images,
        string? preferredLanguage)
    {
        return images
            .Where(image => !string.IsNullOrWhiteSpace(image.FilePath))
            .OrderBy(image => GetLanguageRank(image.Language, preferredLanguage))
            .ThenByDescending(image => image.VoteAverage)
            .ThenByDescending(image => image.VoteCount)
            .ThenBy(image => image.FilePath, StringComparer.Ordinal)
            .Select(ToImageResult)
            .ToList();
    }

    private static ImageResult ToImageResult(ProviderImageResult image) =>
        new(
            image.FilePath,
            ImageGalleryServiceHelper.NormalizeLanguage(image.Language),
            image.AspectRatio,
            image.Width,
            image.Height,
            image.VoteAverage,
            image.VoteCount);

    private static int GetLanguageRank(string? imageLanguage, string? preferredLanguage)
    {
        var normalizedImageLanguage = ImageGalleryServiceHelper.NormalizeLanguage(imageLanguage);
        var normalizedPreferredLanguage = ImageGalleryServiceHelper.NormalizeLanguage(preferredLanguage);

        if (normalizedPreferredLanguage is not null
            && string.Equals(normalizedImageLanguage, normalizedPreferredLanguage, StringComparison.Ordinal))
        {
            return 0;
        }

        if (normalizedImageLanguage is null)
        {
            return 1;
        }

        if (string.Equals(normalizedImageLanguage, "en", StringComparison.Ordinal))
        {
            return 2;
        }

        return 3;
    }
}
