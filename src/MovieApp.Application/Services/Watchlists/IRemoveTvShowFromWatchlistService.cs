namespace MovieApp.Application.Services.Watchlists;

public interface IRemoveTvShowFromWatchlistService
{
    Task RemoveAsync(Guid watchlistId, Guid tvShowId, CancellationToken cancellationToken = default);
}
