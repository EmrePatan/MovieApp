using Microsoft.Extensions.Options;
using MovieApp.Application.Abstractions.Persistence;
using MovieApp.Application.Configuration;
using MovieApp.Application.Models.Identity;

namespace MovieApp.Application.Services.Identity;

public sealed class UserAvatarPresentationService(
    IUserAvatarReadRepository avatarReadRepository,
    IOptions<AvatarStorageOptions> avatarStorageOptions) : IUserAvatarPresentationService
{
    public UserAvatarPresentation Present(UserAvatarSources sources) =>
        UserAvatarResolver.Resolve(sources, ResolveCustomUrl);

    public async Task<UserAvatarPresentation> GetForUserAsync(
        Guid userId,
        CancellationToken cancellationToken = default)
    {
        var sources = await avatarReadRepository.GetSourcesForUserAsync(userId, cancellationToken);
        if (sources is null)
        {
            return new UserAvatarPresentation(null, null, null, UserAvatarKind.Initials);
        }

        return Present(sources);
    }

    public async Task<IReadOnlyDictionary<Guid, UserAvatarPresentation>> GetForUsersAsync(
        IReadOnlyCollection<Guid> userIds,
        CancellationToken cancellationToken = default)
    {
        if (userIds.Count == 0)
        {
            return new Dictionary<Guid, UserAvatarPresentation>();
        }

        var sourcesByUser = await avatarReadRepository.GetSourcesForUsersAsync(userIds, cancellationToken);
        var result = new Dictionary<Guid, UserAvatarPresentation>(sourcesByUser.Count);
        foreach (var pair in sourcesByUser)
        {
            result[pair.Key] = Present(pair.Value);
        }

        return result;
    }

    private string? ResolveCustomUrl(string? storageKey) =>
        UserAvatarUrlBuilder.BuildPublicUrl(avatarStorageOptions.Value.PublicBaseUrl, storageKey);
}
