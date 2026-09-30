using MovieApp.Application.Abstractions.Caching;
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
    IUserExternalLoginRepository externalLoginRepository,
    IUserStatisticsRepository userStatisticsRepository,
    IProfileStatisticsCache profileStatisticsCache,
    IPasswordHasher passwordHasher,
    IAuthenticationSessionService authenticationSessionService,
    IAccountReauthenticationService accountReauthenticationService,
    IUserAvatarPresentationService avatarPresentationService,
    IUserAvatarService avatarService) : IUserProfileService
{
    private const string InvalidCurrentPasswordMessage = "Current password is incorrect.";

    public async Task<UserProfileResult> GetCurrentProfileAsync(CancellationToken cancellationToken = default)
    {
        var user = await GetCurrentUserForReadAsync(cancellationToken);
        var linkedProviders = await externalLoginRepository.GetProvidersForUserAsync(user.Id, cancellationToken);
        var avatar = await avatarPresentationService.GetForUserAsync(user.Id, cancellationToken);

        return UserMapper.ToUserProfileResult(user, linkedProviders, avatar);
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
        var linkedProviders = await externalLoginRepository.GetProvidersForUserAsync(user.Id, cancellationToken);
        var avatar = await avatarPresentationService.GetForUserAsync(user.Id, cancellationToken);

        return UserMapper.ToUserProfileResult(user, linkedProviders, avatar);
    }

    public async Task<AuthenticationResult> ChangePasswordAsync(
        string currentPassword,
        string newPassword,
        CancellationToken cancellationToken = default)
    {
        ValidatePasswordChangeRequest(currentPassword, newPassword);

        var user = await GetCurrentUserForUpdateAsync(cancellationToken);
        if (!user.HasPassword)
        {
            throw new ValidationException("This account does not have a password.");
        }

        EnsureCurrentPassword(user, currentPassword);

        if (passwordHasher.VerifyPassword(newPassword, user.PasswordHash!))
        {
            throw new ValidationException("New password must be different from the current password.");
        }

        var passwordHash = passwordHasher.HashPassword(newPassword);
        user.ChangePassword(passwordHash, DateTime.UtcNow);
        await userRepository.UpdateAsync(user, cancellationToken);

        await authenticationSessionService.RevokeAllRefreshTokensForUserAsync(user.Id, cancellationToken);
        return await authenticationSessionService.IssueAsync(user, cancellationToken);
    }

    public async Task<UserStatisticsResult> GetStatisticsAsync(
        string? timeZoneId = null,
        CancellationToken cancellationToken = default)
    {
        var userId = CurrentUserGuard.RequireUserId(currentUser);
        var cached = await profileStatisticsCache.GetAsync(userId, timeZoneId, cancellationToken);
        if (cached is not null)
        {
            return cached;
        }

        var statistics = await userStatisticsRepository.GetStatisticsAsync(userId, timeZoneId, cancellationToken);
        await profileStatisticsCache.SetAsync(userId, timeZoneId, statistics, cancellationToken);
        return statistics;
    }

    public async Task DeleteAccountAsync(DeleteAccountCommand request, CancellationToken cancellationToken = default)
    {
        var user = await GetCurrentUserForUpdateAsync(cancellationToken);
        var linkedProviders = await externalLoginRepository.GetProvidersForUserAsync(user.Id, cancellationToken);

        if (user.HasPassword)
        {
            var proof = new AccountReauthenticationProof(request.CurrentPassword, null, null);
            await accountReauthenticationService.EnsureCurrentAccountReauthenticatedAsync(
                user,
                linkedProviders,
                proof,
                cancellationToken);
        }
        else if (linkedProviders.Count > 0)
        {
            var proof = new AccountReauthenticationProof(null, request.Provider, request.IdentityToken);
            await accountReauthenticationService.EnsureCurrentAccountReauthenticatedAsync(
                user,
                linkedProviders,
                proof,
                cancellationToken);
        }
        else
        {
            throw new ValidationException("Social re-authentication is required.");
        }

        var avatarKey = user.CustomAvatarStorageKey;
        var deleted = await userRepository.DeleteAsync(user.Id, cancellationToken);
        if (!deleted)
        {
            throw new NotFoundException("The authenticated user was not found.");
        }

        await avatarService.BestEffortDeleteCustomAvatarAsync(avatarKey, cancellationToken);
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
        if (!user.HasPassword || !passwordHasher.VerifyPassword(currentPassword, user.PasswordHash!))
        {
            throw new ValidationException(InvalidCurrentPasswordMessage);
        }
    }
}
