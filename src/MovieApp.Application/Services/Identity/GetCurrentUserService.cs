using MovieApp.Application.Abstractions.Identity;
using MovieApp.Application.Abstractions.Persistence;
using MovieApp.Application.Exceptions;
using MovieApp.Application.Mapping;
using MovieApp.Application.Models.Identity;

namespace MovieApp.Application.Services.Identity;

public sealed class GetCurrentUserService(
    ICurrentUser currentUser,
    IUserRepository userRepository,
    IUserAvatarPresentationService avatarPresentationService) : IGetCurrentUserService
{
    public async Task<CurrentUserResult> GetCurrentUserAsync(CancellationToken cancellationToken = default)
    {
        if (!currentUser.IsAuthenticated || currentUser.UserId is null)
        {
            throw new AuthenticationException("Authentication is required.");
        }

        var user = await userRepository.GetByIdAsync(currentUser.UserId.Value, cancellationToken);
        if (user is null)
        {
            throw new NotFoundException("The authenticated user was not found.");
        }

        var avatar = await avatarPresentationService.GetForUserAsync(user.Id, cancellationToken);
        return UserMapper.ToCurrentUserResult(user, avatar);
    }
}
