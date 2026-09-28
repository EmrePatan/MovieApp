using MovieApp.Application.Models.WatchlistShare;

namespace MovieApp.Application.Services.WatchlistShare;

public interface IWatchlistShareService
{
    Task<WatchlistShareStatusResult> GetStatusAsync(
        Guid watchlistId,
        CancellationToken cancellationToken = default);

    Task<WatchlistShareEnableResult> EnableAsync(Guid watchlistId, CancellationToken cancellationToken = default);

    Task DisableAsync(Guid watchlistId, CancellationToken cancellationToken = default);

    Task<WatchlistShareRotateResult> RotateAsync(Guid watchlistId, CancellationToken cancellationToken = default);

    Task<PublicWatchlistShareResult?> TryGetPublicByTokenAsync(
        string rawToken,
        CancellationToken cancellationToken = default);
}
