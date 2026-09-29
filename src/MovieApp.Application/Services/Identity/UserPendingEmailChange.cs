using MovieApp.Domain.Entities;
using MovieApp.Domain.Users;

namespace MovieApp.Application.Services.Identity;

internal static class UserPendingEmailChange
{
    public static string? GetDisplayPendingEmail(User user) =>
        string.IsNullOrWhiteSpace(user.PendingEmail) ? null : user.PendingEmail.Trim();

    public static bool TokenMatchesUserPendingEmail(User user, string? tokenPendingEmail)
    {
        if (string.IsNullOrWhiteSpace(tokenPendingEmail))
        {
            return string.IsNullOrWhiteSpace(user.PendingEmail);
        }

        if (string.IsNullOrWhiteSpace(user.PendingEmail))
        {
            return false;
        }

        return UserEmailNormalizer.Normalize(tokenPendingEmail) ==
               UserEmailNormalizer.Normalize(user.PendingEmail);
    }
}
