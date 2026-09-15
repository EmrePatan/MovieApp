using Microsoft.EntityFrameworkCore;
using MovieApp.Application.Abstractions.Persistence;
using MovieApp.Application.Models.ReleaseDetection;
using MovieApp.Domain.Entities;

namespace MovieApp.Infrastructure.Persistence.Repositories;

public sealed class CatalogReleaseEventRepository(ApplicationDbContext dbContext) : ICatalogReleaseEventRepository
{
    public async Task<HashSet<string>> GetDedupeKeysForTvShowAsync(
        Guid tvShowId,
        CancellationToken cancellationToken = default)
    {
        var dedupeKeys = await dbContext.CatalogReleaseEvents
            .AsNoTracking()
            .Where(releaseEvent => releaseEvent.TvShowId == tvShowId)
            .Select(releaseEvent => releaseEvent.DedupeKey)
            .ToListAsync(cancellationToken);

        return dedupeKeys.ToHashSet(StringComparer.Ordinal);
    }

    public async Task<bool> ExistsByDedupeKeyAsync(
        string dedupeKey,
        CancellationToken cancellationToken = default)
    {
        return await dbContext.CatalogReleaseEvents
            .AsNoTracking()
            .AnyAsync(releaseEvent => releaseEvent.DedupeKey == dedupeKey, cancellationToken);
    }

    public async Task<CatalogReleaseEventInsertResult> TryAddEventsAsync(
        IReadOnlyList<CatalogReleaseEvent> events,
        CancellationToken cancellationToken = default)
    {
        if (events.Count == 0)
        {
            return CatalogReleaseEventInsertResult.Empty;
        }

        dbContext.CatalogReleaseEvents.AddRange(events);

        try
        {
            await dbContext.SaveChangesAsync(cancellationToken);

            return new CatalogReleaseEventInsertResult(
                events.Count,
                0,
                events.Select(releaseEvent => releaseEvent.Id).ToList());
        }
        catch (DbUpdateException exception) when (DbUpdateExceptionExtensions.IsUniqueConstraintViolation(exception))
        {
            DetachTrackedReleaseEvents();

            var createdEventIds = new List<Guid>();
            var alreadyExisted = 0;

            foreach (var releaseEvent in events)
            {
                if (await TryAddSingleAsync(releaseEvent, cancellationToken))
                {
                    createdEventIds.Add(releaseEvent.Id);
                }
                else
                {
                    alreadyExisted++;
                }
            }

            return new CatalogReleaseEventInsertResult(
                createdEventIds.Count,
                alreadyExisted,
                createdEventIds);
        }
    }

    private async Task<bool> TryAddSingleAsync(
        CatalogReleaseEvent releaseEvent,
        CancellationToken cancellationToken)
    {
        try
        {
            dbContext.CatalogReleaseEvents.Add(releaseEvent);
            await dbContext.SaveChangesAsync(cancellationToken);
            return true;
        }
        catch (DbUpdateException exception) when (DbUpdateExceptionExtensions.IsUniqueConstraintViolation(exception))
        {
            DetachIfTracked(releaseEvent);
            return false;
        }
    }

    private void DetachTrackedReleaseEvents()
    {
        foreach (var entry in dbContext.ChangeTracker.Entries<CatalogReleaseEvent>().ToList())
        {
            entry.State = EntityState.Detached;
        }
    }

    private void DetachIfTracked(CatalogReleaseEvent releaseEvent)
    {
        var entry = dbContext.Entry(releaseEvent);
        if (entry.State != EntityState.Detached)
        {
            entry.State = EntityState.Detached;
        }
    }
}
