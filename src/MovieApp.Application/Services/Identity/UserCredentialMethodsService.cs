using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using MovieApp.Application.Abstractions.Identity;
using MovieApp.Application.Abstractions.Persistence;
using MovieApp.Application.Configuration;
using MovieApp.Application.Exceptions;
using MovieApp.Application.Identity;
using MovieApp.Application.Mapping;
using MovieApp.Application.Models.Identity;
using MovieApp.Application.Services.Localization;
using MovieApp.Application.Validation;
using MovieApp.Domain.Entities;
using MovieApp.Domain.Users;

namespace MovieApp.Application.Services.Identity;

public sealed class UserCredentialMethodsService(
    ICurrentUser currentUser,
    IUserRepository userRepository,
    IUserExternalLoginRepository externalLoginRepository,
    IAccountReauthenticationService accountReauthenticationService,
    IPasswordHasher passwordHasher,
    IAuthenticationSessionService authenticationSessionService,
    IEmailVerificationTokenRepository emailVerificationTokenRepository,
    IEmailVerificationDeliverySecretProtector deliverySecretProtector,
    IEmailVerificationDeliveryEnqueuer deliveryEnqueuer,
    IOptions<EmailVerificationOptions> emailVerificationOptions,
    ILogger<UserCredentialMethodsService> logger) : IUserCredentialMethodsService
{
    public const string EmailChangeRequestedMessage =
        "We sent a verification link to your new email address. Your current email stays active until you confirm the change.";

    public async Task<UserProfileResult> LinkExternalLoginAsync(
        LinkExternalLoginCommand command,
        CancellationToken cancellationToken = default)
    {
        var user = await GetCurrentUserForUpdateAsync(cancellationToken);
        var linkedProviders = await externalLoginRepository.GetProvidersForUserAsync(user.Id, cancellationToken);

        await accountReauthenticationService.EnsureCurrentAccountReauthenticatedAsync(
            user,
            linkedProviders,
            command.CurrentAccountProof,
            cancellationToken);

        var targetIdentity = await accountReauthenticationService.VerifySocialIdentityTokenAsync(
            command.TargetProvider,
            command.TargetIdentityToken,
            cancellationToken);

        await accountReauthenticationService.EnsureTargetProviderCanBeLinkedAsync(
            targetIdentity.Provider,
            targetIdentity.Subject,
            user.Id,
            linkedProviders,
            cancellationToken);

        var externalLogin = UserExternalLogin.Create(
            Guid.NewGuid(),
            user.Id,
            targetIdentity.Provider,
            targetIdentity.Subject,
            targetIdentity.Email,
            DateTime.UtcNow);

        await externalLoginRepository.CreateAsync(externalLogin, cancellationToken);

        linkedProviders = await externalLoginRepository.GetProvidersForUserAsync(user.Id, cancellationToken);
        return UserMapper.ToUserProfileResult(user, linkedProviders);
    }

    public async Task<UserProfileResult> UnlinkExternalLoginAsync(
        UnlinkExternalLoginCommand command,
        CancellationToken cancellationToken = default)
    {
        var provider = ExternalLoginProviders.Normalize(command.Provider);
        var user = await GetCurrentUserForUpdateAsync(cancellationToken);
        var linkedProviders = await externalLoginRepository.GetProvidersForUserAsync(user.Id, cancellationToken);

        if (!linkedProviders.Contains(provider, StringComparer.Ordinal))
        {
            throw new ValidationException(
                "The selected sign-in provider is not linked to this account.",
                AccountAuthenticationErrorCodes.ProviderNotLinked);
        }

        if (!SignInMethodPolicy.CanUnlinkProvider(user.HasPassword, linkedProviders.Count))
        {
            throw new ConflictException(
                "You must keep at least one sign-in method on your account.",
                AccountAuthenticationErrorCodes.FinalSignInMethodCannotBeRemoved);
        }

        await accountReauthenticationService.EnsureCurrentAccountReauthenticatedAsync(
            user,
            linkedProviders,
            command.CurrentAccountProof,
            cancellationToken);

        var deleted = await externalLoginRepository.DeleteAsync(user.Id, provider, cancellationToken);
        if (!deleted)
        {
            throw new ValidationException(
                "The selected sign-in provider is not linked to this account.",
                AccountAuthenticationErrorCodes.ProviderNotLinked);
        }

        linkedProviders = await externalLoginRepository.GetProvidersForUserAsync(user.Id, cancellationToken);
        return UserMapper.ToUserProfileResult(user, linkedProviders);
    }

    public async Task<AuthenticationResult> CreatePasswordAsync(
        CreatePasswordCommand command,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(command.Provider) || string.IsNullOrWhiteSpace(command.IdentityToken))
        {
            throw new ValidationException("Social re-authentication is required.");
        }

        var passwordValidation = PasswordPolicyValidator.Validate(command.NewPassword);
        if (!passwordValidation.IsValid)
        {
            throw new ValidationException(passwordValidation.ErrorMessage!);
        }

        var user = await GetCurrentUserForUpdateAsync(cancellationToken);
        if (user.HasPassword)
        {
            throw new ConflictException("This account already has a password.");
        }

        if (!SignInMethodPolicy.HasRealVerifiedEmailForPasswordLogin(user.Email, user.IsEmailVerified))
        {
            throw new ValidationException(
                "Add and verify an email address before creating a password.",
                AccountAuthenticationErrorCodes.PasswordLoginRequiresVerifiedEmail);
        }

        var linkedProviders = await externalLoginRepository.GetProvidersForUserAsync(user.Id, cancellationToken);
        if (linkedProviders.Count == 0)
        {
            throw new ValidationException("Social re-authentication is required.");
        }

        var proof = new AccountReauthenticationProof(null, command.Provider, command.IdentityToken);
        await accountReauthenticationService.EnsureCurrentAccountReauthenticatedAsync(
            user,
            linkedProviders,
            proof,
            cancellationToken);

        var passwordHash = passwordHasher.HashPassword(command.NewPassword);
        user.ChangePassword(passwordHash, DateTime.UtcNow);
        await userRepository.UpdateAsync(user, cancellationToken);

        await authenticationSessionService.RevokeAllRefreshTokensForUserAsync(user.Id, cancellationToken);
        return await authenticationSessionService.IssueAsync(user, cancellationToken);
    }

    public async Task<MessageResult> RequestEmailChangeAsync(
        RequestEmailChangeCommand command,
        CancellationToken cancellationToken = default)
    {
        var emailValidation = ProfileValidator.ValidateEmail(command.NewEmail);
        if (!emailValidation.IsValid)
        {
            throw new ValidationException(emailValidation.ErrorMessage!);
        }

        var user = await GetCurrentUserForUpdateAsync(cancellationToken);
        var linkedProviders = await externalLoginRepository.GetProvidersForUserAsync(user.Id, cancellationToken);

        await accountReauthenticationService.EnsureCurrentAccountReauthenticatedAsync(
            user,
            linkedProviders,
            command.CurrentAccountProof,
            cancellationToken);

        var normalizedEmail = UserEmailNormalizer.Normalize(command.NewEmail);
        if (user.NormalizedEmail == normalizedEmail)
        {
            return new MessageResult(EmailChangeRequestedMessage);
        }

        if (await userRepository.ExistsByNormalizedEmailAsync(normalizedEmail, cancellationToken))
        {
            throw new ConflictException("A user with this email address already exists.");
        }

        var normalizedContentLocale = ContentLocaleResolver.ResolveFromAcceptLanguage(command.ContentLocale);
        var utcNow = DateTime.UtcNow;

        await emailVerificationTokenRepository.InvalidateActiveTokensForUserAsync(user.Id, utcNow, cancellationToken);

        var rawToken = PasswordResetTokenGenerator.GenerateToken();
        var tokenHash = PasswordResetTokenHasher.HashToken(rawToken);
        var lifetime = TimeSpan.FromMinutes(
            Math.Max(1, emailVerificationOptions.Value.TokenLifetimeMinutes));

        var verificationToken = new EmailVerificationToken
        {
            Id = Guid.NewGuid(),
            UserId = user.Id,
            TokenHash = tokenHash,
            CreatedAtUtc = utcNow,
            ExpiresAtUtc = utcNow.Add(lifetime),
            ProtectedDeliverySecret = deliverySecretProtector.Protect(rawToken),
            ContentLocale = normalizedContentLocale,
            PendingEmail = command.NewEmail.Trim(),
        };

        await emailVerificationTokenRepository.CreateAsync(verificationToken, cancellationToken);

        try
        {
            await deliveryEnqueuer.EnqueueAsync(verificationToken.Id, cancellationToken);
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch (Exception exception)
        {
            EmailVerificationLogMessages.LogDeliveryEnqueueFailed(
                logger,
                verificationToken.Id,
                user.Id,
                exception.GetType().Name);
        }

        return new MessageResult(EmailChangeRequestedMessage);
    }

    private async Task<User> GetCurrentUserForUpdateAsync(CancellationToken cancellationToken)
    {
        var userId = CurrentUserGuard.RequireUserId(currentUser);
        var user = await userRepository.GetByIdForUpdateAsync(userId, cancellationToken);

        if (user is null || !user.IsActive)
        {
            throw new NotFoundException("The authenticated user was not found.");
        }

        return user;
    }
}
