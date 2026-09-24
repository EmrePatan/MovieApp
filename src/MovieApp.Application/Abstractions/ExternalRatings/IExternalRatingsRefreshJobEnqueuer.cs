using MovieApp.Domain.Enums;

namespace MovieApp.Application.Abstractions.ExternalRatings;

public interface IExternalRatingsRefreshJobEnqueuer
{
    void EnqueueRefresh(CatalogContentType mediaType, int tmdbId);
}
