using MovieApp.Application.Abstractions.Persistence;
using MovieApp.Application.Abstractions.TvShows;
using MovieApp.Domain.Enums;

namespace MovieApp.Application.Services.TvShows;

public sealed class TvShowCatalogSyncStateService(
    ITvShowCatalogSyncStateRepository repository) : ITvShowCatalogSyncStateService
{
    public Task MarkRefreshedAsync(
        Guid tvShowId,
        TvShowCatalogRefreshReason reason,
        DateTime refreshedAtUtc,
        CancellationToken cancellationToken = default) =>
        repository.MarkRefreshedAsync(tvShowId, reason, refreshedAtUtc, cancellationToken);

    public Task MarkRefreshedBatchAsync(
        IReadOnlyList<Guid> tvShowIds,
        TvShowCatalogRefreshReason reason,
        DateTime refreshedAtUtc,
        CancellationToken cancellationToken = default) =>
        repository.MarkRefreshedBatchAsync(tvShowIds, reason, refreshedAtUtc, cancellationToken);

    public Task MarkChangeSignalAsync(
        Guid tvShowId,
        DateOnly changeSignalDate,
        DateTime updatedAtUtc,
        CancellationToken cancellationToken = default) =>
        repository.MarkChangeSignalAsync(tvShowId, changeSignalDate, updatedAtUtc, cancellationToken);

    public Task MarkChangesSyncAsync(
        Guid tvShowId,
        DateTime refreshedAtUtc,
        DateOnly changeSignalDate,
        CancellationToken cancellationToken = default) =>
        repository.MarkChangesSyncAsync(tvShowId, refreshedAtUtc, changeSignalDate, cancellationToken);

    public Task MarkHotReleaseAsync(
        Guid tvShowId,
        DateTime refreshedAtUtc,
        DateTime? nextHotCheckAtUtc,
        CancellationToken cancellationToken = default) =>
        repository.MarkHotReleaseAsync(tvShowId, refreshedAtUtc, nextHotCheckAtUtc, cancellationToken);

    public Task UpdateNextHotCheckAsync(
        Guid tvShowId,
        DateTime? nextHotCheckAtUtc,
        DateTime updatedAtUtc,
        CancellationToken cancellationToken = default) =>
        repository.UpdateNextHotCheckAsync(tvShowId, nextHotCheckAtUtc, updatedAtUtc, cancellationToken);
}
