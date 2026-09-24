using Microsoft.EntityFrameworkCore;
using MovieApp.Application.Abstractions.Persistence;
using MovieApp.Application.Models.ExternalRatings;
using MovieApp.Application.Services.ExternalRatings;
using MovieApp.Domain.Entities;
using MovieApp.Domain.Enums;

namespace MovieApp.Infrastructure.Persistence.Repositories;

public sealed class ExternalRatingSnapshotRepository(ApplicationDbContext dbContext) : IExternalRatingSnapshotRepository
{
    public async Task<ExternalRatingSnapshot?> GetAsync(
        CatalogContentType mediaType,
        int tmdbId,
        ExternalRatingsProvider provider,
        CancellationToken cancellationToken = default) =>
        await dbContext.ExternalRatingSnapshots
            .AsNoTracking()
            .FirstOrDefaultAsync(
                snapshot =>
                    snapshot.MediaType == mediaType
                    && snapshot.TmdbId == tmdbId
                    && snapshot.Provider == provider,
                cancellationToken);

    public async Task UpsertAsync(
        CatalogContentType mediaType,
        int tmdbId,
        ExternalRatingsProvider provider,
        ExternalRatingSnapshotPayload payload,
        DateTime fetchedAtUtc,
        CancellationToken cancellationToken = default)
    {
        var existing = await dbContext.ExternalRatingSnapshots
            .FirstOrDefaultAsync(
                snapshot =>
                    snapshot.MediaType == mediaType
                    && snapshot.TmdbId == tmdbId
                    && snapshot.Provider == provider,
                cancellationToken);

        var payloadJson = ExternalRatingSnapshotSerializer.Serialize(payload);

        if (existing is null)
        {
            dbContext.ExternalRatingSnapshots.Add(new ExternalRatingSnapshot
            {
                Id = Guid.NewGuid(),
                MediaType = mediaType,
                TmdbId = tmdbId,
                Provider = provider,
                PayloadJson = payloadJson,
                FetchedAtUtc = fetchedAtUtc
            });
        }
        else
        {
            existing.PayloadJson = payloadJson;
            existing.FetchedAtUtc = fetchedAtUtc;
        }

        await dbContext.SaveChangesAsync(cancellationToken);
    }
}
