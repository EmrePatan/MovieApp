using MovieApp.Application.Models.Identity;
using MovieApp.Application.Services.Identity;
using MovieApp.Domain.Entities;

namespace MovieApp.Application.Mapping;

public static class UserMapper
{
    public static CurrentUserResult ToCurrentUserResult(User user, UserAvatarPresentation? avatar = null) =>
        new(
            user.Id,
            user.Email,
            user.UserName,
            user.DisplayName,
            user.CreatedAt,
            avatar?.EffectiveAvatarUrl);

    public static UserProfileResult ToUserProfileResult(
        User user,
        IReadOnlyList<string> linkedProviders,
        UserAvatarPresentation? avatar = null) =>
        new(
            user.Id,
            user.Email,
            user.UserName,
            user.DisplayName,
            user.CreatedAt,
            user.HasPassword,
            linkedProviders,
            UserPendingEmailChange.GetDisplayPendingEmail(user),
            avatar?.CustomAvatarUrl,
            avatar?.ProviderAvatarUrl,
            avatar?.EffectiveAvatarUrl,
            avatar?.AvatarKind ?? UserAvatarKind.Initials);

    public static TokenUserContext ToTokenUserContext(User user) =>
        new(user.Id, user.Email, user.SecurityStamp);
}
