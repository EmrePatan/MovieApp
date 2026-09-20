using MovieApp.Application.Models.Identity;
using MovieApp.Domain.Entities;

namespace MovieApp.Application.Mapping;

public static class UserMapper
{
    public static CurrentUserResult ToCurrentUserResult(User user) =>
        new(
            user.Id,
            user.Email,
            user.UserName,
            user.DisplayName,
            user.CreatedAt);

    public static UserProfileResult ToUserProfileResult(
        User user,
        IReadOnlyList<string> linkedProviders) =>
        new(
            user.Id,
            user.Email,
            user.UserName,
            user.DisplayName,
            user.CreatedAt,
            user.HasPassword,
            linkedProviders);

    public static TokenUserContext ToTokenUserContext(User user) =>
        new(user.Id, user.Email, user.SecurityStamp);
}
