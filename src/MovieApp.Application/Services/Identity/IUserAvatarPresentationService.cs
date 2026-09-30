using MovieApp.Application.Models.Identity;

namespace MovieApp.Application.Services.Identity;

public interface IUserAvatarPresentationService
{
    UserAvatarPresentation Present(UserAvatarSources sources);

    Task<UserAvatarPresentation> GetForUserAsync(Guid userId, CancellationToken cancellationToken = default);

    Task<IReadOnlyDictionary<Guid, UserAvatarPresentation>> GetForUsersAsync(
        IReadOnlyCollection<Guid> userIds,
        CancellationToken cancellationToken = default);
}
