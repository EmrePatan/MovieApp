using MovieApp.Domain.Enums;

namespace MovieApp.Application.Services.ExternalRatings;

internal static class ExternalRatingsRefreshKeys
{
    public static string Build(CatalogContentType mediaType, int tmdbId) =>
        $"external-ratings:mdblist:{mediaType}:{tmdbId}";
}
