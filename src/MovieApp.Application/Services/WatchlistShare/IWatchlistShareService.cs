using MovieApp.Application.Models.WatchlistShare;

namespace MovieApp.Application.Services.WatchlistShare;

public interface IWatchlistShareService
{
    Task<WatchlistShareStatusResult> GetStatusAsync(CancellationToken cancellationToken = default);

    Task<WatchlistShareEnableResult> EnableAsync(CancellationToken cancellationToken = default);

    Task DisableAsync(CancellationToken cancellationToken = default);

    Task<WatchlistShareRotateResult> RotateAsync(CancellationToken cancellationToken = default);

    Task<PublicWatchlistShareResult?> TryGetPublicByTokenAsync(
        string rawToken,
        CancellationToken cancellationToken = default);
}
