using MovieApp.Application.Models.Watchlists;

namespace MovieApp.Application.Services.Watchlists;

public interface IGetWatchlistMembershipService
{
    Task<WatchlistMembershipResult> GetMovieMembershipAsync(
        Guid movieId,
        CancellationToken cancellationToken = default);

    Task<WatchlistMembershipResult> GetTvShowMembershipAsync(
        Guid tvShowId,
        CancellationToken cancellationToken = default);
}
