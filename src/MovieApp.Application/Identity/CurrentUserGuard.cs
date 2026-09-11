using MovieApp.Application.Abstractions.Identity;
using MovieApp.Application.Exceptions;

namespace MovieApp.Application.Identity;

internal static class CurrentUserGuard
{
    internal static Guid RequireUserId(ICurrentUser currentUser)
    {
        if (!currentUser.IsAuthenticated || currentUser.UserId is null)
        {
            throw new AuthenticationException("Authentication is required.");
        }

        return currentUser.UserId.Value;
    }
}
