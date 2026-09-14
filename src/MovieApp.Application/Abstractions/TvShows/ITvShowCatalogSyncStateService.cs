using MovieApp.Domain.Enums;

namespace MovieApp.Application.Abstractions.TvShows;

public interface ITvShowCatalogSyncStateService
{
    Task MarkRefreshedAsync(
        Guid tvShowId,
        TvShowCatalogRefreshReason reason,
        DateTime refreshedAtUtc,
        CancellationToken cancellationToken = default);

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
}
