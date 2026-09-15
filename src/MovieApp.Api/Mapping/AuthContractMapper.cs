using MovieApp.Application.Models.Identity;
using MovieApp.Contracts.Auth;

namespace MovieApp.Api.Mapping;

public static class AuthContractMapper
{
    public static RegisterUserRequest ToRegisterUserRequest(RegisterRequest request) =>
        new(request.Email, request.Password, request.DisplayName);

    public static LoginUserRequest ToLoginUserRequest(LoginRequest request) =>
        new(request.Email, request.Password);

    public static Application.Models.Identity.SocialAuthRequest ToSocialAuthRequest(
        Contracts.Auth.SocialAuthRequest request) =>
        new(request.Provider, request.IdentityToken);

    public static AuthResponse ToAuthResponse(AuthenticationResult result) =>
        new(
            result.AccessToken,
            result.ExpiresAt,
            ToCurrentUserResponse(result.User));

    public static CurrentUserResponse ToCurrentUserResponse(CurrentUserResult result) =>
        new(
            result.Id,
            result.Email,
            result.UserName,
            result.DisplayName,
            result.CreatedAt);

    public static Application.Models.Identity.ForgotPasswordRequest ToForgotPasswordRequest(
        Contracts.Auth.ForgotPasswordRequest request) =>
        new(request.Email);

    public static Application.Models.Identity.ResetPasswordRequest ToResetPasswordRequest(
        Contracts.Auth.ResetPasswordRequest request) =>
        new(request.Token, request.NewPassword);

    public static MessageResponse ToMessageResponse(MessageResult result) =>
        new(result.Message);
}
