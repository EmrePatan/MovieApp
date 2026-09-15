using MovieApp.Application.Abstractions.Identity;
using MovieApp.Application.Abstractions.Persistence;
using MovieApp.Application.Exceptions;
using MovieApp.Application.Mapping;
using MovieApp.Application.Models.Identity;
using MovieApp.Application.Validation;
using MovieApp.Domain.Entities;
using MovieApp.Domain.Users;

namespace MovieApp.Application.Services.Identity;

public sealed class SocialAuthService(
    IUserExternalLoginRepository externalLoginRepository,
    IUserRepository userRepository,
    IEnumerable<ISocialIdentityTokenVerifier> tokenVerifiers,
    ITokenService tokenService) : ISocialAuthService
{
    private const string ExistingPasswordAccountMessage =
        "An account with this email already exists. Sign in with your password to continue.";

    private readonly IReadOnlyDictionary<string, ISocialIdentityTokenVerifier> _tokenVerifiers =
        tokenVerifiers.ToDictionary(verifier => verifier.Provider, StringComparer.Ordinal);

    public async Task<AuthenticationResult> AuthenticateAsync(
        SocialAuthRequest request,
        CancellationToken cancellationToken = default)
    {
        var validationResult = SocialAuthValidator.Validate(request);
        if (!validationResult.IsValid)
        {
            throw new ValidationException(validationResult.ErrorMessage!);
        }

        var provider = ExternalLoginProviders.Normalize(request.Provider);
        if (!_tokenVerifiers.TryGetValue(provider, out var verifier))
        {
            throw new ValidationException("Unsupported social provider.");
        }

        VerifiedSocialIdentity identity;
        try
        {
            identity = await verifier.VerifyIdentityTokenAsync(request.IdentityToken, cancellationToken);
        }
        catch (AuthenticationException)
        {
            throw;
        }
        catch (Exception)
        {
            throw new AuthenticationException("Social authentication failed.");
        }

        if (!string.Equals(identity.Provider, provider, StringComparison.Ordinal))
        {
            throw new AuthenticationException("Social authentication failed.");
        }

        var existingUser = await externalLoginRepository.GetUserByProviderAndSubjectAsync(
            provider,
            identity.Subject,
            cancellationToken);

        if (existingUser is not null)
        {
            return await IssueAuthenticationResultAsync(existingUser, identity, cancellationToken);
        }

        if (identity.IsEmailVerified && !string.IsNullOrWhiteSpace(identity.Email))
        {
            var normalizedEmail = UserEmailNormalizer.Normalize(identity.Email);
            var userByEmail = await userRepository.GetByNormalizedEmailAsync(normalizedEmail, cancellationToken);

            if (userByEmail is not null)
            {
                if (userByEmail.HasPassword)
                {
                    throw new ConflictException(ExistingPasswordAccountMessage);
                }

                return await LinkExternalLoginAndAuthenticateAsync(
                    userByEmail,
                    identity,
                    cancellationToken);
            }
        }

        return await CreateUserAndAuthenticateAsync(identity, cancellationToken);
    }

    private async Task<AuthenticationResult> LinkExternalLoginAndAuthenticateAsync(
        User user,
        VerifiedSocialIdentity identity,
        CancellationToken cancellationToken)
    {
        if (!user.IsActive)
        {
            throw new AuthenticationException("This account is inactive.");
        }

        user.SetInitialDisplayNameIfEmpty(identity.DisplayName, DateTime.UtcNow);

        var externalLogin = UserExternalLogin.Create(
            Guid.NewGuid(),
            user.Id,
            identity.Provider,
            identity.Subject,
            identity.Email,
            DateTime.UtcNow);

        try
        {
            await externalLoginRepository.CreateAsync(externalLogin, cancellationToken);
        }
        catch (ConflictException)
        {
            var linkedUser = await externalLoginRepository.GetUserByProviderAndSubjectAsync(
                identity.Provider,
                identity.Subject,
                cancellationToken);

            if (linkedUser is null)
            {
                throw;
            }

            return await IssueAuthenticationResultAsync(linkedUser, identity, cancellationToken);
        }

        await userRepository.UpdateAsync(user, cancellationToken);

        return await IssueAuthenticationResultAsync(user, identity, cancellationToken);
    }

    private async Task<AuthenticationResult> CreateUserAndAuthenticateAsync(
        VerifiedSocialIdentity identity,
        CancellationToken cancellationToken)
    {
        var utcNow = DateTime.UtcNow;
        var email = ResolveAccountEmail(identity);
        var displayName = ResolveDisplayName(identity, email);

        var user = User.CreateFromExternalIdentity(
            Guid.NewGuid(),
            email,
            displayName,
            utcNow);

        var externalLogin = UserExternalLogin.Create(
            Guid.NewGuid(),
            user.Id,
            identity.Provider,
            identity.Subject,
            identity.Email,
            utcNow);

        try
        {
            await userRepository.CreateAsync(user, cancellationToken);
            await externalLoginRepository.CreateAsync(externalLogin, cancellationToken);
        }
        catch (ConflictException)
        {
            var linkedUser = await externalLoginRepository.GetUserByProviderAndSubjectAsync(
                identity.Provider,
                identity.Subject,
                cancellationToken);

            if (linkedUser is not null)
            {
                return await IssueAuthenticationResultAsync(linkedUser, identity, cancellationToken);
            }

            if (identity.IsEmailVerified && !string.IsNullOrWhiteSpace(identity.Email))
            {
                var normalizedEmail = UserEmailNormalizer.Normalize(identity.Email);
                var userByEmail = await userRepository.GetByNormalizedEmailAsync(normalizedEmail, cancellationToken);
                if (userByEmail is not null && !userByEmail.HasPassword)
                {
                    return await LinkExternalLoginAndAuthenticateAsync(userByEmail, identity, cancellationToken);
                }

                if (userByEmail is not null && userByEmail.HasPassword)
                {
                    throw new ConflictException(ExistingPasswordAccountMessage);
                }
            }

            throw;
        }

        return IssueAuthenticationResult(user);
    }

    private async Task<AuthenticationResult> IssueAuthenticationResultAsync(
        User user,
        VerifiedSocialIdentity identity,
        CancellationToken cancellationToken)
    {
        if (!user.IsActive)
        {
            throw new AuthenticationException("This account is inactive.");
        }

        user.SetInitialDisplayNameIfEmpty(identity.DisplayName, DateTime.UtcNow);
        user.RecordSuccessfulLogin(DateTime.UtcNow);
        await userRepository.UpdateAsync(user, cancellationToken);

        return IssueAuthenticationResult(user);
    }

    private AuthenticationResult IssueAuthenticationResult(User user)
    {
        var token = tokenService.CreateAccessToken(UserMapper.ToTokenUserContext(user));

        return new AuthenticationResult(
            token.AccessToken,
            token.ExpiresAt,
            UserMapper.ToCurrentUserResult(user));
    }

    private static string ResolveAccountEmail(VerifiedSocialIdentity identity)
    {
        if (!string.IsNullOrWhiteSpace(identity.Email))
        {
            return identity.Email.Trim();
        }

        return $"{identity.Provider}+{identity.Subject}@external.movieapp.local";
    }

    private static string ResolveDisplayName(VerifiedSocialIdentity identity, string email)
    {
        if (!string.IsNullOrWhiteSpace(identity.DisplayName))
        {
            return identity.DisplayName.Trim();
        }

        var atIndex = email.IndexOf('@');
        return atIndex > 0 ? email[..atIndex] : email;
    }
}
