using Microsoft.EntityFrameworkCore;
using MovieApp.Application.Abstractions.Persistence;
using MovieApp.Domain.Entities;
using MovieApp.Domain.Enums;

namespace MovieApp.Infrastructure.Persistence.Repositories;

public sealed class TvShowCatalogSyncStateRepository(ApplicationDbContext dbContext)
    : ITvShowCatalogSyncStateRepository
{
    private const int MaxUpsertAttempts = 3;

    public Task MarkRefreshedAsync(
        Guid tvShowId,
        TvShowCatalogRefreshReason reason,
        DateTime refreshedAtUtc,
        CancellationToken cancellationToken = default) =>
        UpsertAsync(
            tvShowId,
            refreshedAtUtc,
            reason,
            null,
            null,
            updateNextHotCheck: false,
            cancellationToken);

    public async Task MarkRefreshedBatchAsync(
        IReadOnlyList<Guid> tvShowIds,
        TvShowCatalogRefreshReason reason,
        DateTime refreshedAtUtc,
        CancellationToken cancellationToken = default)
    {
        if (tvShowIds.Count == 0)
        {
            return;
        }

        var distinctIds = tvShowIds.Distinct().ToList();
        var existingStates = await dbContext.TvShowCatalogSyncStates
            .Where(state => distinctIds.Contains(state.TvShowId))
            .ToDictionaryAsync(state => state.TvShowId, cancellationToken);

        var hasChanges = false;

        foreach (var tvShowId in distinctIds)
        {
            if (existingStates.TryGetValue(tvShowId, out var state))
            {
                if (!ShouldApplyRefresh(state.LastRefreshedAtUtc, refreshedAtUtc))
                {
                    continue;
                }

                state.LastRefreshedAtUtc = refreshedAtUtc;
                state.LastRefreshReason = reason;
                state.UpdatedAtUtc = refreshedAtUtc;
                hasChanges = true;
                continue;
            }

            dbContext.TvShowCatalogSyncStates.Add(new TvShowCatalogSyncState
            {
                TvShowId = tvShowId,
                LastRefreshedAtUtc = refreshedAtUtc,
                LastRefreshReason = reason,
                UpdatedAtUtc = refreshedAtUtc
            });
            hasChanges = true;
        }

        if (!hasChanges)
        {
            return;
        }

        await dbContext.SaveChangesAsync(cancellationToken);
    }

    public Task MarkChangeSignalAsync(
        Guid tvShowId,
        DateOnly changeSignalDate,
        DateTime updatedAtUtc,
        CancellationToken cancellationToken = default) =>
        UpsertAsync(
            tvShowId,
            null,
            null,
            changeSignalDate,
            null,
            updateNextHotCheck: false,
            cancellationToken,
            updatedAtUtc);

    public Task MarkChangesSyncAsync(
        Guid tvShowId,
        DateTime refreshedAtUtc,
        DateOnly changeSignalDate,
        CancellationToken cancellationToken = default) =>
        UpsertAsync(
            tvShowId,
            refreshedAtUtc,
            TvShowCatalogRefreshReason.ChangesSync,
            changeSignalDate,
            null,
            updateNextHotCheck: false,
            cancellationToken);

    public Task MarkHotReleaseAsync(
        Guid tvShowId,
        DateTime refreshedAtUtc,
        DateTime? nextHotCheckAtUtc,
        CancellationToken cancellationToken = default) =>
        UpsertAsync(
            tvShowId,
            refreshedAtUtc,
            TvShowCatalogRefreshReason.HotRelease,
            null,
            nextHotCheckAtUtc,
            updateNextHotCheck: true,
            cancellationToken);

    public Task UpdateNextHotCheckAsync(
        Guid tvShowId,
        DateTime? nextHotCheckAtUtc,
        DateTime updatedAtUtc,
        CancellationToken cancellationToken = default) =>
        UpsertAsync(
            tvShowId,
            null,
            null,
            null,
            nextHotCheckAtUtc,
            updateNextHotCheck: true,
            cancellationToken,
            updatedAtUtc);

    public async Task MarkUpcomingEpisodeSyncAsync(
        Guid tvShowId,
        DateTime syncedAtUtc,
        CancellationToken cancellationToken = default)
    {
        for (var attempt = 1; attempt <= MaxUpsertAttempts; attempt++)
        {
            var state = await dbContext.TvShowCatalogSyncStates
                .FirstOrDefaultAsync(existing => existing.TvShowId == tvShowId, cancellationToken);

            if (state is null)
            {
                state = new TvShowCatalogSyncState
                {
                    TvShowId = tvShowId,
                    LastUpcomingEpisodeSyncAtUtc = syncedAtUtc,
                    UpdatedAtUtc = syncedAtUtc
                };
                dbContext.TvShowCatalogSyncStates.Add(state);
            }
            else
            {
                state.LastUpcomingEpisodeSyncAtUtc = syncedAtUtc;
                state.UpdatedAtUtc = syncedAtUtc;
            }

            try
            {
                await dbContext.SaveChangesAsync(cancellationToken);
                return;
            }
            catch (DbUpdateException) when (attempt < MaxUpsertAttempts)
            {
                dbContext.ChangeTracker.Clear();
            }
        }

        throw new InvalidOperationException(
            $"Failed to mark upcoming episode sync for TV show '{tvShowId}'.");
    }

    internal static bool ShouldApplyRefresh(DateTime? existingRefreshedAtUtc, DateTime incomingRefreshedAtUtc) =>
        !existingRefreshedAtUtc.HasValue || incomingRefreshedAtUtc >= existingRefreshedAtUtc.Value;

    internal static bool ShouldApplyChangeSignal(DateOnly? existingSignalDate, DateOnly incomingSignalDate) =>
        !existingSignalDate.HasValue || incomingSignalDate >= existingSignalDate.Value;

    private async Task UpsertAsync(
        Guid tvShowId,
        DateTime? refreshedAtUtc,
        TvShowCatalogRefreshReason? refreshReason,
        DateOnly? changeSignalDate,
        DateTime? nextHotCheckAtUtc,
        bool updateNextHotCheck,
        CancellationToken cancellationToken,
        DateTime? explicitUpdatedAtUtc = null)
    {
        for (var attempt = 1; attempt <= MaxUpsertAttempts; attempt++)
        {
            var state = await dbContext.TvShowCatalogSyncStates
                .FirstOrDefaultAsync(existing => existing.TvShowId == tvShowId, cancellationToken);

            var updatedAtUtc = explicitUpdatedAtUtc ?? refreshedAtUtc ?? DateTime.UtcNow;

            if (state is null)
            {
                if (refreshedAtUtc is null && changeSignalDate is null && !updateNextHotCheck)
                {
                    return;
                }

                state = new TvShowCatalogSyncState
                {
                    TvShowId = tvShowId,
                    LastRefreshedAtUtc = refreshedAtUtc,
                    LastRefreshReason = refreshReason,
                    LastChangeSignalDate = changeSignalDate,
                    NextHotCheckAtUtc = updateNextHotCheck ? nextHotCheckAtUtc : null,
                    UpdatedAtUtc = updatedAtUtc
                };

                dbContext.TvShowCatalogSyncStates.Add(state);
            }
            else
            {
                var hasChanges = false;

                if (refreshedAtUtc.HasValue &&
                    refreshReason.HasValue &&
                    ShouldApplyRefresh(state.LastRefreshedAtUtc, refreshedAtUtc.Value))
                {
                    state.LastRefreshedAtUtc = refreshedAtUtc.Value;
                    state.LastRefreshReason = refreshReason.Value;
                    state.UpdatedAtUtc = updatedAtUtc;
                    hasChanges = true;
                }

                if (changeSignalDate.HasValue &&
                    ShouldApplyChangeSignal(state.LastChangeSignalDate, changeSignalDate.Value))
                {
                    state.LastChangeSignalDate = changeSignalDate.Value;
                    state.UpdatedAtUtc = updatedAtUtc;
                    hasChanges = true;
                }

                if (updateNextHotCheck && state.NextHotCheckAtUtc != nextHotCheckAtUtc)
                {
                    state.NextHotCheckAtUtc = nextHotCheckAtUtc;
                    state.UpdatedAtUtc = updatedAtUtc;
                    hasChanges = true;
                }

                if (!hasChanges)
                {
                    return;
                }
            }

            try
            {
                await dbContext.SaveChangesAsync(cancellationToken);
                return;
            }
            catch (DbUpdateException) when (attempt < MaxUpsertAttempts)
            {
                dbContext.ChangeTracker.Clear();
            }
        }

        throw new InvalidOperationException(
            $"Failed to upsert TV show catalog sync state for TV show '{tvShowId}'.");
    }
}
