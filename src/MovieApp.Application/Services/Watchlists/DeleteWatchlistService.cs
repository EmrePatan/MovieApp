using MovieApp.Application.Abstractions.Identity;
using MovieApp.Application.Abstractions.Persistence;
using MovieApp.Application.Exceptions;
using MovieApp.Application.Identity;

namespace MovieApp.Application.Services.Watchlists;

public sealed class DeleteWatchlistService(
    ICurrentUser currentUser,
    IWatchlistRepository watchlistRepository) : IDeleteWatchlistService
{
    public async Task DeleteAsync(Guid watchlistId, CancellationToken cancellationToken = default)
    {
        var userId = CurrentUserGuard.RequireUserId(currentUser);

        var deleted = await watchlistRepository.DeleteAsync(userId, watchlistId, cancellationToken);
        if (!deleted)
        {
            throw new NotFoundException("The requested watchlist was not found.");
        }
    }
}
