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
    IResendVerificationService resendVerificationService,
    IEnumerable<ISocialIdentityTokenVerifier> tokenVerifiers) : IUserProfileService
{
    private const string InvalidCurrentPasswordMessage = "Current password is incorrect.";
    private const string SocialReauthenticationRequiredMessage =
        "Social re-authentication is required.";
    private const string SocialReauthenticationFailedMessage = "Social re-authentication failed.";
    private const string PasswordConfirmationRequiredMessage =
        "Confirm deletion with your current password.";
    private const string PasswordNotUsedMessage =
        "This account does not use a password. Confirm deletion with a linked sign-in provider.";
    private const string UnsupportedProviderMessage = "Unsupported social provider.";
    private const string UnlinkedProviderMessage =
        "The selected sign-in provider is not linked to this account.";

    private readonly Dictionary<string, ISocialIdentityTokenVerifier> _tokenVerifiers =
        tokenVerifiers.ToDictionary(verifier => verifier.Provider, StringComparer.Ordinal);

    public async Task<UserProfileResult> GetCurrentProfileAsync(CancellationToken cancellationToken = default)
    {
        var user = await GetCurrentUserForReadAsync(cancellationToken);
        var linkedProviders = await externalLoginRepository.GetProvidersForUserAsync(user.Id, cancellationToken);
        return UserMapper.ToUserProfileResult(user, linkedProviders);
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

        return UserMapper.ToUserProfileResult(user, linkedProviders);
    }

    public async Task<AuthenticationResult> ChangeEmailAsync(
        string email,
        string currentPassword,
        string contentLocale,
        CancellationToken cancellationToken = default)
    {
        ValidateEmailChangeRequest(email, currentPassword);

        var user = await GetCurrentUserForUpdateAsync(cancellationToken);
        EnsureCurrentPassword(user, currentPassword);

        var normalizedEmail = UserEmailNormalizer.Normalize(email);
        if (user.NormalizedEmail == normalizedEmail)
        {
            return await CreateAuthenticationResult(user);
        }

        if (await userRepository.ExistsByNormalizedEmailAsync(normalizedEmail, cancellationToken))
        {
            throw new ConflictException("A user with this email address already exists.");
        }

        user.ChangeEmail(email, normalizedEmail, DateTime.UtcNow);
        await userRepository.UpdateAsync(user, cancellationToken);
        await resendVerificationService.SendVerificationEmailAsync(
            user,
            contentLocale,
            cancellationToken);

        await authenticationSessionService.RevokeAllRefreshTokensForUserAsync(user.Id, cancellationToken);
        return await CreateAuthenticationResult(user);
    }

    public async Task<AuthenticationResult> ChangePasswordAsync(
        string currentPassword,
        string newPassword,
        CancellationToken cancellationToken = default)
    {
        ValidatePasswordChangeRequest(currentPassword, newPassword);

        var user = await GetCurrentUserForUpdateAsync(cancellationToken);
        EnsureCurrentPassword(user, currentPassword);

        if (user.HasPassword && passwordHasher.VerifyPassword(newPassword, user.PasswordHash!))
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
            EnsurePasswordDeletionConfirmation(request);
            EnsureCurrentPassword(user, request.CurrentPassword!);
        }
        else if (linkedProviders.Count > 0)
        {
            await EnsureSocialDeletionConfirmationAsync(user, request, linkedProviders, cancellationToken);
        }
        else
        {
            throw new ValidationException(SocialReauthenticationRequiredMessage);
        }

        var deleted = await userRepository.DeleteAsync(user.Id, cancellationToken);
        if (!deleted)
        {
            throw new NotFoundException("The authenticated user was not found.");
        }
    }

    private static void EnsurePasswordDeletionConfirmation(DeleteAccountCommand request)
    {
        if (!string.IsNullOrWhiteSpace(request.Provider) || !string.IsNullOrWhiteSpace(request.IdentityToken))
        {
            throw new ValidationException(PasswordConfirmationRequiredMessage);
        }

        var passwordValidation = ProfileValidator.ValidateCurrentPassword(request.CurrentPassword);
        if (!passwordValidation.IsValid)
        {
            throw new ValidationException(passwordValidation.ErrorMessage!);
        }
    }

    private async Task EnsureSocialDeletionConfirmationAsync(
        Domain.Entities.User user,
        DeleteAccountCommand request,
        IReadOnlyList<string> linkedProviders,
        CancellationToken cancellationToken)
    {
        if (!string.IsNullOrWhiteSpace(request.CurrentPassword))
        {
            throw new ValidationException(PasswordNotUsedMessage);
        }

        if (string.IsNullOrWhiteSpace(request.Provider) || string.IsNullOrWhiteSpace(request.IdentityToken))
        {
            throw new ValidationException(SocialReauthenticationRequiredMessage);
        }

        var provider = ExternalLoginProviders.Normalize(request.Provider);
        if (!linkedProviders.Contains(provider, StringComparer.Ordinal))
        {
            throw new ValidationException(UnlinkedProviderMessage);
        }

        if (!_tokenVerifiers.TryGetValue(provider, out var verifier))
        {
            throw new ValidationException(UnsupportedProviderMessage);
        }

        VerifiedSocialIdentity identity;
        try
        {
            identity = await verifier.VerifyIdentityTokenAsync(request.IdentityToken, cancellationToken);
        }
        catch (AuthenticationException)
        {
            throw new AuthenticationException(SocialReauthenticationFailedMessage);
        }
        catch (Exception)
        {
            throw new AuthenticationException(SocialReauthenticationFailedMessage);
        }

        if (!string.Equals(identity.Provider, provider, StringComparison.Ordinal))
        {
            throw new AuthenticationException(SocialReauthenticationFailedMessage);
        }

        var authenticatedUser = await externalLoginRepository.GetUserByProviderAndSubjectAsync(
            provider,
            identity.Subject,
            cancellationToken);

        if (authenticatedUser is null || authenticatedUser.Id != user.Id)
        {
            throw new AuthenticationException(SocialReauthenticationFailedMessage);
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
        if (!user.HasPassword || !passwordHasher.VerifyPassword(currentPassword, user.PasswordHash!))
        {
            throw new ValidationException(InvalidCurrentPasswordMessage);
        }
    }

    private Task<AuthenticationResult> CreateAuthenticationResult(Domain.Entities.User user) =>
        authenticationSessionService.IssueAsync(user);
}
