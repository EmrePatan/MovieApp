using MovieApp.Application.Abstractions.Identity;
using MovieApp.Application.Abstractions.Persistence;
using MovieApp.Application.Exceptions;
using MovieApp.Application.Identity;
using MovieApp.Application.Models.Identity;
using MovieApp.Application.Validation;
using MovieApp.Domain.Entities;
using MovieApp.Domain.Users;

namespace MovieApp.Application.Services.Identity;

public sealed class AccountReauthenticationService(
    IUserExternalLoginRepository externalLoginRepository,
    IPasswordHasher passwordHasher,
    IEnumerable<ISocialIdentityTokenVerifier> tokenVerifiers) : IAccountReauthenticationService
{
    private const string InvalidCurrentPasswordMessage = "Current password is incorrect.";
    private const string SocialReauthenticationRequiredMessage = "Social re-authentication is required.";
    private const string SocialReauthenticationFailedMessage = "Social re-authentication failed.";
    private const string PasswordConfirmationRequiredMessage = "Confirm with your current password.";
    private const string PasswordNotUsedMessage =
        "This account does not use a password. Confirm with a linked sign-in provider.";
    private const string UnsupportedProviderMessage = "Unsupported social provider.";
    private const string UnlinkedProviderMessage = "The selected sign-in provider is not linked to this account.";

    private readonly Dictionary<string, ISocialIdentityTokenVerifier> _tokenVerifiers =
        tokenVerifiers.ToDictionary(verifier => verifier.Provider, StringComparer.Ordinal);

    public async Task EnsureCurrentAccountReauthenticatedAsync(
        User user,
        IReadOnlyList<string> linkedProviders,
        AccountReauthenticationProof proof,
        CancellationToken cancellationToken = default)
    {
        if (user.HasPassword)
        {
            EnsurePasswordProofOnly(proof);
            EnsureCurrentPassword(user, proof.CurrentPassword!);
            return;
        }

        if (linkedProviders.Count > 0)
        {
            await EnsureSocialProofAsync(user, proof, linkedProviders, cancellationToken);
            return;
        }

        throw new ValidationException(SocialReauthenticationRequiredMessage);
    }

    public async Task<VerifiedSocialIdentity> VerifySocialIdentityTokenAsync(
        string provider,
        string identityToken,
        CancellationToken cancellationToken = default)
    {
        var normalizedProvider = ExternalLoginProviders.Normalize(provider);
        if (!_tokenVerifiers.TryGetValue(normalizedProvider, out var verifier))
        {
            throw new ValidationException(UnsupportedProviderMessage);
        }

        if (string.IsNullOrWhiteSpace(identityToken))
        {
            throw new ValidationException(SocialReauthenticationRequiredMessage);
        }

        VerifiedSocialIdentity identity;
        try
        {
            identity = await verifier.VerifyIdentityTokenAsync(identityToken, cancellationToken);
        }
        catch (AuthenticationException)
        {
            throw new AuthenticationException(SocialReauthenticationFailedMessage);
        }
        catch (Exception)
        {
            throw new AuthenticationException(SocialReauthenticationFailedMessage);
        }

        if (!string.Equals(identity.Provider, normalizedProvider, StringComparison.Ordinal))
        {
            throw new AuthenticationException(SocialReauthenticationFailedMessage);
        }

        return identity;
    }

    public async Task EnsureTargetProviderCanBeLinkedAsync(
        string provider,
        string providerSubject,
        Guid currentUserId,
        IReadOnlyList<string> linkedProviders,
        CancellationToken cancellationToken = default)
    {
        var normalizedProvider = ExternalLoginProviders.Normalize(provider);

        if (linkedProviders.Contains(normalizedProvider, StringComparer.Ordinal))
        {
            throw new ConflictException(
                "This provider is already linked to your account.",
                AccountAuthenticationErrorCodes.ProviderAlreadyLinked);
        }

        var owner = await externalLoginRepository.GetUserByProviderAndSubjectAsync(
            normalizedProvider,
            providerSubject,
            cancellationToken);

        if (owner is not null && owner.Id != currentUserId)
        {
            throw new ConflictException(
                "This social account is already linked to another MovieApp user.",
                AccountAuthenticationErrorCodes.TargetProviderLinkedToAnotherUser);
        }
    }

    private static void EnsurePasswordProofOnly(AccountReauthenticationProof proof)
    {
        if (!string.IsNullOrWhiteSpace(proof.Provider) || !string.IsNullOrWhiteSpace(proof.IdentityToken))
        {
            throw new ValidationException(PasswordConfirmationRequiredMessage);
        }

        var passwordValidation = ProfileValidator.ValidateCurrentPassword(proof.CurrentPassword);
        if (!passwordValidation.IsValid)
        {
            throw new ValidationException(passwordValidation.ErrorMessage!);
        }
    }

    private void EnsureCurrentPassword(User user, string currentPassword)
    {
        if (!user.HasPassword || !passwordHasher.VerifyPassword(currentPassword, user.PasswordHash!))
        {
            throw new ValidationException(InvalidCurrentPasswordMessage);
        }
    }

    private async Task EnsureSocialProofAsync(
        User user,
        AccountReauthenticationProof proof,
        IReadOnlyList<string> linkedProviders,
        CancellationToken cancellationToken)
    {
        if (!string.IsNullOrWhiteSpace(proof.CurrentPassword))
        {
            throw new ValidationException(PasswordNotUsedMessage);
        }

        if (string.IsNullOrWhiteSpace(proof.Provider) || string.IsNullOrWhiteSpace(proof.IdentityToken))
        {
            throw new ValidationException(SocialReauthenticationRequiredMessage);
        }

        var provider = ExternalLoginProviders.Normalize(proof.Provider);
        if (!linkedProviders.Contains(provider, StringComparer.Ordinal))
        {
            throw new ValidationException(UnlinkedProviderMessage);
        }

        var identity = await VerifySocialIdentityTokenAsync(provider, proof.IdentityToken, cancellationToken);
        var authenticatedUser = await externalLoginRepository.GetUserByProviderAndSubjectAsync(
            identity.Provider,
            identity.Subject,
            cancellationToken);

        if (authenticatedUser is null || authenticatedUser.Id != user.Id)
        {
            throw new AuthenticationException(SocialReauthenticationFailedMessage);
        }
    }
}
