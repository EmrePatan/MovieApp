namespace MovieApp.Application.Services.Watchlists;

public interface IRemoveMovieFromWatchlistService
{
    Task RemoveAsync(Guid watchlistId, Guid movieId, CancellationToken cancellationToken = default);
}
