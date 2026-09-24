using MovieApp.Domain.Enums;

namespace MovieApp.Application.Services.ExternalRatings;

public interface IExternalRatingsRefreshService
{
    Task RefreshAsync(
        CatalogContentType mediaType,
        int tmdbId,
        CancellationToken cancellationToken = default);
}
