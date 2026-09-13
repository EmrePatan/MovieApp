using MovieApp.Application.Abstractions.Identity;
using MovieApp.Application.Abstractions.Persistence;
using MovieApp.Application.Identity;
using MovieApp.Application.Models.Watchlists;

namespace MovieApp.Application.Services.Watchlists;

public sealed class GetWatchlistMembershipService(
    ICurrentUser currentUser,
    IWatchlistItemRepository watchlistItemRepository) : IGetWatchlistMembershipService
{
    public async Task<WatchlistMembershipResult> GetMovieMembershipAsync(
        Guid movieId,
        CancellationToken cancellationToken = default)
    {
        var userId = CurrentUserGuard.RequireUserId(currentUser);
        var watchlistIds = await watchlistItemRepository.GetWatchlistIdsContainingMovieAsync(
            userId,
            movieId,
            cancellationToken);

        return new WatchlistMembershipResult(watchlistIds, watchlistIds.Count > 0);
    }

    public async Task<WatchlistMembershipResult> GetTvShowMembershipAsync(
        Guid tvShowId,
        CancellationToken cancellationToken = default)
    {
        var userId = CurrentUserGuard.RequireUserId(currentUser);
        var watchlistIds = await watchlistItemRepository.GetWatchlistIdsContainingTvShowAsync(
            userId,
            tvShowId,
            cancellationToken);

        return new WatchlistMembershipResult(watchlistIds, watchlistIds.Count > 0);
    }
}
