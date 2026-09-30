using MovieApp.Application.Models.Identity;

namespace MovieApp.Application.Services.Identity;

public interface IUserAvatarService
{
    Task<UserProfileResult> UploadCustomAvatarAsync(
        Stream uploadStream,
        long? declaredContentLength,
        CancellationToken cancellationToken = default);

    Task<UserProfileResult> RemoveCustomAvatarAsync(CancellationToken cancellationToken = default);

    Task BestEffortDeleteCustomAvatarAsync(string? storageKey, CancellationToken cancellationToken = default);
}
