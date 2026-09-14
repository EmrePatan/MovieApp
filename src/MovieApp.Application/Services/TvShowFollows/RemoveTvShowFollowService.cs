using MovieApp.Application.Abstractions.Identity;
using MovieApp.Application.Abstractions.Persistence;
using MovieApp.Application.Identity;

namespace MovieApp.Application.Services.TvShowFollows;

public sealed class RemoveTvShowFollowService(
    ICurrentUser currentUser,
    ITvShowFollowRepository tvShowFollowRepository) : IRemoveTvShowFollowService
{
    public async Task RemoveAsync(Guid tvShowId, CancellationToken cancellationToken = default)
    {
        var userId = CurrentUserGuard.RequireUserId(currentUser);
        await tvShowFollowRepository.RemoveForTvShowAsync(userId, tvShowId, cancellationToken);
    }
}
