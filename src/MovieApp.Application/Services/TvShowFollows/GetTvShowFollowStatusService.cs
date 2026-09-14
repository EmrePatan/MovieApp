using MovieApp.Application.Abstractions.Identity;
using MovieApp.Application.Abstractions.Persistence;
using MovieApp.Application.Identity;
using MovieApp.Application.Models.TvShowFollows;

namespace MovieApp.Application.Services.TvShowFollows;

public sealed class GetTvShowFollowStatusService(
    ICurrentUser currentUser,
    ITvShowFollowRepository tvShowFollowRepository) : IGetTvShowFollowStatusService
{
    public async Task<TvShowFollowStatusResult> GetAsync(
        Guid tvShowId,
        CancellationToken cancellationToken = default)
    {
        var userId = CurrentUserGuard.RequireUserId(currentUser);
        var follow = await tvShowFollowRepository.GetForUserAndTvShowAsync(userId, tvShowId, cancellationToken);

        if (follow is null)
        {
            return new TvShowFollowStatusResult(
                IsFollowing: false,
                NotifyNewSeasons: true,
                NotifyNewEpisodes: true,
                BaselineEstablished: false);
        }

        return new TvShowFollowStatusResult(
            IsFollowing: true,
            NotifyNewSeasons: follow.NotifyNewSeasons,
            NotifyNewEpisodes: follow.NotifyNewEpisodes,
            BaselineEstablished: follow.IsBaselineEstablished);
    }
}
