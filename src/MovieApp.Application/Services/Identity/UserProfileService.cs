using MovieApp.Application.Abstractions.Identity;
using MovieApp.Application.Abstractions.Persistence;
using MovieApp.Application.Exceptions;
using MovieApp.Application.Identity;
using MovieApp.Application.Mapping;
using MovieApp.Application.Models.Identity;
using MovieApp.Application.Validation;
using MovieApp.Domain.Users;

namespace MovieApp.Application.Services.Identity;

public sealed class UserProfileService(
    ICurrentUser currentUser,
    IUserRepository userRepository,
    IUserStatisticsRepository userStatisticsRepository,
    IPasswordHasher passwordHasher,
    ITokenService tokenService) : IUserProfileService
{
    private const string InvalidCurrentPasswordMessage = "Current password is incorrect.";

    public async Task<UserProfileResult> GetCurrentProfileAsync(CancellationToken cancellationToken = default)
    {
        var user = await GetCurrentUserForReadAsync(cancellationToken);
        return UserMapper.ToUserProfileResult(user);
    }

    public async Task<UserProfileResult> UpdateDisplayNameAsync(
        string displayName,
        CancellationToken cancellationToken = default)
    {
        var validation = ProfileValidator.ValidateDisplayName(displayName);
        if (!validation.IsValid)
        {
            throw new ValidationException(validation.ErrorMessage!);
        }

        var user = await GetCurrentUserForUpdateAsync(cancellationToken);
        user.UpdateDisplayName(displayName, DateTime.UtcNow);
        await userRepository.UpdateAsync(user, cancellationToken);

        return UserMapper.ToUserProfileResult(user);
    }

    public async Task<AuthenticationResult> ChangeEmailAsync(
        string email,
        string currentPassword,
        CancellationToken cancellationToken = default)
    {
        ValidateEmailChangeRequest(email, currentPassword);

        var user = await GetCurrentUserForUpdateAsync(cancellationToken);
        EnsureCurrentPassword(user, currentPassword);

        var normalizedEmail = UserEmailNormalizer.Normalize(email);
        if (user.NormalizedEmail == normalizedEmail)
        {
            return CreateAuthenticationResult(user);
        }

        if (await userRepository.ExistsByNormalizedEmailAsync(normalizedEmail, cancellationToken))
        {
            throw new ConflictException("A user with this email address already exists.");
        }

        user.ChangeEmail(email, normalizedEmail, DateTime.UtcNow);
        await userRepository.UpdateAsync(user, cancellationToken);

        return CreateAuthenticationResult(user);
    }

    public async Task<AuthenticationResult> ChangePasswordAsync(
        string currentPassword,
        string newPassword,
        CancellationToken cancellationToken = default)
    {
        ValidatePasswordChangeRequest(currentPassword, newPassword);

        var user = await GetCurrentUserForUpdateAsync(cancellationToken);
        EnsureCurrentPassword(user, currentPassword);

        if (passwordHasher.VerifyPassword(newPassword, user.PasswordHash))
        {
            throw new ValidationException("New password must be different from the current password.");
        }

        var passwordHash = passwordHasher.HashPassword(newPassword);
        user.ChangePassword(passwordHash, DateTime.UtcNow);
        await userRepository.UpdateAsync(user, cancellationToken);

        return CreateAuthenticationResult(user);
    }

    public async Task<UserStatisticsResult> GetStatisticsAsync(CancellationToken cancellationToken = default)
    {
        var userId = CurrentUserGuard.RequireUserId(currentUser);
        return await userStatisticsRepository.GetStatisticsAsync(userId, cancellationToken);
    }

    public async Task DeleteAccountAsync(string currentPassword, CancellationToken cancellationToken = default)
    {
        var passwordValidation = ProfileValidator.ValidateCurrentPassword(currentPassword);
        if (!passwordValidation.IsValid)
        {
            throw new ValidationException(passwordValidation.ErrorMessage!);
        }

        var user = await GetCurrentUserForUpdateAsync(cancellationToken);
        EnsureCurrentPassword(user, currentPassword);

        var deleted = await userRepository.DeleteAsync(user.Id, cancellationToken);
        if (!deleted)
        {
            throw new NotFoundException("The authenticated user was not found.");
        }
    }

    private static void ValidateEmailChangeRequest(string email, string currentPassword)
    {
        var emailValidation = ProfileValidator.ValidateEmail(email);
        if (!emailValidation.IsValid)
        {
            throw new ValidationException(emailValidation.ErrorMessage!);
        }

        var passwordValidation = ProfileValidator.ValidateCurrentPassword(currentPassword);
        if (!passwordValidation.IsValid)
        {
            throw new ValidationException(passwordValidation.ErrorMessage!);
        }
    }

    private static void ValidatePasswordChangeRequest(string currentPassword, string newPassword)
    {
        var currentPasswordValidation = ProfileValidator.ValidateCurrentPassword(currentPassword);
        if (!currentPasswordValidation.IsValid)
        {
            throw new ValidationException(currentPasswordValidation.ErrorMessage!);
        }

        var newPasswordValidation = PasswordPolicyValidator.Validate(newPassword);
        if (!newPasswordValidation.IsValid)
        {
            throw new ValidationException(newPasswordValidation.ErrorMessage!);
        }
    }

    private async Task<Domain.Entities.User> GetCurrentUserForReadAsync(CancellationToken cancellationToken)
    {
        var userId = CurrentUserGuard.RequireUserId(currentUser);
        var user = await userRepository.GetByIdAsync(userId, cancellationToken);

        if (user is null || !user.IsActive)
        {
            throw new NotFoundException("The authenticated user was not found.");
        }

        return user;
    }

    private async Task<Domain.Entities.User> GetCurrentUserForUpdateAsync(CancellationToken cancellationToken)
    {
        var userId = CurrentUserGuard.RequireUserId(currentUser);
        var user = await userRepository.GetByIdForUpdateAsync(userId, cancellationToken);

        if (user is null || !user.IsActive)
        {
            throw new NotFoundException("The authenticated user was not found.");
        }

        return user;
    }

    private void EnsureCurrentPassword(Domain.Entities.User user, string currentPassword)
    {
        if (!passwordHasher.VerifyPassword(currentPassword, user.PasswordHash))
        {
            throw new ValidationException(InvalidCurrentPasswordMessage);
        }
    }

    private AuthenticationResult CreateAuthenticationResult(Domain.Entities.User user)
    {
        var token = tokenService.CreateAccessToken(UserMapper.ToTokenUserContext(user));
        return new AuthenticationResult(
            token.AccessToken,
            token.ExpiresAt,
            UserMapper.ToCurrentUserResult(user));
    }
}
