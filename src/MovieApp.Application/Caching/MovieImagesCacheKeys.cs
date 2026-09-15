using MovieApp.Application.Services.Images;

namespace MovieApp.Application.Caching;

public static class MovieImagesCacheKeys
{
    public const string Prefix = "movie-images:";

    public const string Version = "v1";

    public static string Create(Guid movieId, string? language) =>
        $"{Prefix}{movieId}:{ImageGalleryServiceHelper.ToCacheLanguageKey(language)}:{Version}";
}
