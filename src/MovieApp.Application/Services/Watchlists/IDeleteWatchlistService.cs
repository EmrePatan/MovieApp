namespace MovieApp.Application.Services.Watchlists;

public interface IDeleteWatchlistService
{
    Task DeleteAsync(Guid watchlistId, CancellationToken cancellationToken = default);
}
