using MovieApp.Application.Services.Images;

namespace MovieApp.Application.Caching;

public static class TvShowImagesCacheKeys
{
    public const string Prefix = "tvshow-images:";

    public const string Version = "v1";

    public static string Create(Guid tvShowId, string? language) =>
        $"{Prefix}{tvShowId}:{ImageGalleryServiceHelper.ToCacheLanguageKey(language)}:{Version}";
}
