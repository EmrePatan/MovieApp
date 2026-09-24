using MovieApp.Application.Models.ExternalRatings;
using MovieApp.Domain.Entities;
using MovieApp.Domain.Enums;

namespace MovieApp.Application.Abstractions.Persistence;

public interface IExternalRatingSnapshotRepository
{
    Task<ExternalRatingSnapshot?> GetAsync(
        CatalogContentType mediaType,
        int tmdbId,
        ExternalRatingsProvider provider,
        CancellationToken cancellationToken = default);

    Task UpsertAsync(
        CatalogContentType mediaType,
        int tmdbId,
        ExternalRatingsProvider provider,
        ExternalRatingSnapshotPayload payload,
        DateTime fetchedAtUtc,
        CancellationToken cancellationToken = default);
}
