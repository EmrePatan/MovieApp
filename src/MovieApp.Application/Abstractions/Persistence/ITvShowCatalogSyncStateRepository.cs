using MovieApp.Domain.Enums;

namespace MovieApp.Application.Abstractions.Persistence;

public interface ITvShowCatalogSyncStateRepository
{
    Task MarkRefreshedAsync(
        Guid tvShowId,
        TvShowCatalogRefreshReason reason,
        DateTime refreshedAtUtc,
        CancellationToken cancellationToken = default);

    Task MarkRefreshedBatchAsync(
        IReadOnlyList<Guid> tvShowIds,
        TvShowCatalogRefreshReason reason,
        DateTime refreshedAtUtc,
        CancellationToken cancellationToken = default) =>
        throw new NotSupportedException();

    Task MarkChangeSignalAsync(
        Guid tvShowId,
        DateOnly changeSignalDate,
        DateTime updatedAtUtc,
        CancellationToken cancellationToken = default);

    Task MarkChangesSyncAsync(
        Guid tvShowId,
        DateTime refreshedAtUtc,
        DateOnly changeSignalDate,
        CancellationToken cancellationToken = default);

    Task MarkHotReleaseAsync(
        Guid tvShowId,
        DateTime refreshedAtUtc,
        DateTime? nextHotCheckAtUtc,
        CancellationToken cancellationToken = default);

    Task UpdateNextHotCheckAsync(
        Guid tvShowId,
        DateTime? nextHotCheckAtUtc,
        DateTime updatedAtUtc,
        CancellationToken cancellationToken = default);

    Task MarkUpcomingEpisodeSyncAsync(
        Guid tvShowId,
        DateTime syncedAtUtc,
        CancellationToken cancellationToken = default);
}
