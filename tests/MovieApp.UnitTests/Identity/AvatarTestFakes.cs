using MovieApp.Application.Models.Identity;
using MovieApp.Application.Services.Identity;

namespace MovieApp.UnitTests.Identity;

internal sealed class FakeUserAvatarPresentationService : IUserAvatarPresentationService
{
    public UserAvatarPresentation Present(UserAvatarSources sources) =>
        UserAvatarResolver.Resolve(sources, static _ => null);

    public Task<UserAvatarPresentation> GetForUserAsync(Guid userId, CancellationToken cancellationToken = default) =>
        Task.FromResult(new UserAvatarPresentation(null, null, null, UserAvatarKind.Initials));

    public Task<IReadOnlyDictionary<Guid, UserAvatarPresentation>> GetForUsersAsync(
        IReadOnlyCollection<Guid> userIds,
        CancellationToken cancellationToken = default) =>
        Task.FromResult<IReadOnlyDictionary<Guid, UserAvatarPresentation>>(new Dictionary<Guid, UserAvatarPresentation>());
}

internal sealed class TrackingUserAvatarService : IUserAvatarService
{
    public List<string> DeletedKeys { get; } = [];

    public Task<UserProfileResult> UploadCustomAvatarAsync(
        Stream uploadStream,
        long? declaredContentLength,
        CancellationToken cancellationToken = default) =>
        throw new NotSupportedException();

    public Task<UserProfileResult> RemoveCustomAvatarAsync(CancellationToken cancellationToken = default) =>
        throw new NotSupportedException();

    public Task BestEffortDeleteCustomAvatarAsync(string? storageKey, CancellationToken cancellationToken = default)
    {
        if (!string.IsNullOrWhiteSpace(storageKey))
        {
            DeletedKeys.Add(storageKey);
        }

        return Task.CompletedTask;
    }
}
