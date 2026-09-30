using MovieApp.Application.Models.Identity;

namespace MovieApp.Application.Abstractions.Persistence;

public interface IUserAvatarReadRepository
{
    Task<UserAvatarSources?> GetSourcesForUserAsync(Guid userId, CancellationToken cancellationToken = default);

    Task<IReadOnlyDictionary<Guid, UserAvatarSources>> GetSourcesForUsersAsync(
        IReadOnlyCollection<Guid> userIds,
        CancellationToken cancellationToken = default);
}
