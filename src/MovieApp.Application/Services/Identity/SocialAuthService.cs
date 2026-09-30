using System.Diagnostics;
using Microsoft.Extensions.Logging;
using MovieApp.Application.Abstractions.Identity;
using MovieApp.Application.Abstractions.Persistence;
using MovieApp.Application.Exceptions;
using MovieApp.Application.Identity;
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
    IAuthenticationSessionService authenticationSessionService,
    ILogger<SocialAuthService> logger) : ISocialAuthService
{
    private const string SocialAuthenticationFailedMessage = "Social authentication failed.";

    private readonly Dictionary<string, ISocialIdentityTokenVerifier> _tokenVerifiers =
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

        var totalStopwatch = Stopwatch.StartNew();
        var perf = new SocialAuthPerfState();
        var shouldLogPerf = false;

        try
        {
            shouldLogPerf = true;

            VerifiedSocialIdentity identity;
            try
            {
                var tokenStopwatch = Stopwatch.StartNew();
                identity = await verifier.VerifyIdentityTokenAsync(request.IdentityToken, cancellationToken);
                perf.TokenVerificationMs = tokenStopwatch.ElapsedMilliseconds;
            }
            catch (AuthenticationException)
            {
                perf.Complete("authentication_failed");
                throw;
            }
            catch (Exception)
            {
                perf.Complete("authentication_failed");
                throw new AuthenticationException(SocialAuthenticationFailedMessage);
            }

            if (!string.Equals(identity.Provider, provider, StringComparison.Ordinal))
            {
                perf.Complete("authentication_failed");
                throw new AuthenticationException(SocialAuthenticationFailedMessage);
            }

            var lookupStopwatch = Stopwatch.StartNew();
            var existingUser = await externalLoginRepository.GetUserByProviderAndSubjectAsync(
                provider,
                identity.Subject,
                cancellationToken);
            perf.DbLookupMs += lookupStopwatch.ElapsedMilliseconds;

            if (existingUser is not null)
            {
                var result = await IssueAuthenticationResultAsync(existingUser, identity, perf, cancellationToken);
                perf.Complete("existing_login");
                return result;
            }

            if (identity.IsEmailVerified && !string.IsNullOrWhiteSpace(identity.Email))
            {
                lookupStopwatch.Restart();
                var normalizedEmail = UserEmailNormalizer.Normalize(identity.Email);
                var userByEmail = await userRepository.GetByNormalizedEmailAsync(normalizedEmail, cancellationToken);
                perf.DbLookupMs += lookupStopwatch.ElapsedMilliseconds;

                if (userByEmail is not null)
                {
                    perf.Complete("existing_account_different_sign_in_method");
                    throw new AuthenticationException(
                        SocialAuthenticationFailedMessage,
                        AccountAuthenticationErrorCodes.AccountExistsDifferentSignInMethod);
                }
            }

            var createdResult = await CreateUserAndAuthenticateAsync(identity, perf, cancellationToken);
            perf.Complete("new_user");
            return createdResult;
        }
        catch (AuthenticationException)
        {
            if (perf.Outcome is null)
            {
                perf.Complete("authentication_failed");
            }

            throw;
        }
        catch (ConflictException)
        {
            if (perf.Outcome is null)
            {
                perf.Complete("conflict");
            }

            throw;
        }
        finally
        {
            if (shouldLogPerf && perf.Outcome is not null)
            {
                SocialAuthServiceLogMessages.LogPerf(
                    logger,
                    provider,
                    perf.Outcome,
                    totalStopwatch.ElapsedMilliseconds,
                    perf.TokenVerificationMs,
                    perf.DbLookupMs,
                    perf.PersistenceMs,
                    perf.IssueTokenMs);
            }
        }
    }

    private async Task<AuthenticationResult> CreateUserAndAuthenticateAsync(
        VerifiedSocialIdentity identity,
        SocialAuthPerfState perf,
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

        if (ShouldMarkEmailVerified(identity, email))
        {
            user.MarkEmailVerified(utcNow);
        }

        var externalLogin = UserExternalLogin.Create(
            Guid.NewGuid(),
            user.Id,
            identity.Provider,
            identity.Subject,
            identity.Email,
            utcNow);
        externalLogin.SetProviderPictureUrl(identity.ProviderPictureUrl);

        try
        {
            var persistenceStopwatch = Stopwatch.StartNew();
            await userRepository.CreateAsync(user, cancellationToken);
            await externalLoginRepository.CreateAsync(externalLogin, cancellationToken);
            perf.PersistenceMs += persistenceStopwatch.ElapsedMilliseconds;
        }
        catch (ConflictException)
        {
            var lookupStopwatch = Stopwatch.StartNew();
            var linkedUser = await externalLoginRepository.GetUserByProviderAndSubjectAsync(
                identity.Provider,
                identity.Subject,
                cancellationToken);
            perf.DbLookupMs += lookupStopwatch.ElapsedMilliseconds;

            if (linkedUser is not null)
            {
                return await IssueAuthenticationResultAsync(linkedUser, identity, perf, cancellationToken);
            }

            if (identity.IsEmailVerified && !string.IsNullOrWhiteSpace(identity.Email))
            {
                var normalizedEmail = UserEmailNormalizer.Normalize(identity.Email);
                lookupStopwatch.Restart();
                var userByEmail = await userRepository.GetByNormalizedEmailAsync(normalizedEmail, cancellationToken);
                perf.DbLookupMs += lookupStopwatch.ElapsedMilliseconds;

                if (userByEmail is not null)
                {
                    throw new AuthenticationException(
                        SocialAuthenticationFailedMessage,
                        AccountAuthenticationErrorCodes.AccountExistsDifferentSignInMethod);
                }
            }

            throw;
        }

        return await IssueAuthenticationResultAsync(user, perf, cancellationToken);
    }

    private async Task<AuthenticationResult> IssueAuthenticationResultAsync(
        User user,
        VerifiedSocialIdentity identity,
        SocialAuthPerfState perf,
        CancellationToken cancellationToken)
    {
        if (!user.IsActive)
        {
            throw new AuthenticationException("This account is inactive.");
        }

        user.SetInitialDisplayNameIfEmpty(identity.DisplayName, DateTime.UtcNow);
        user.RecordSuccessfulLogin(DateTime.UtcNow);

        var persistenceStopwatch = Stopwatch.StartNew();
        await userRepository.UpdateAsync(user, cancellationToken);
        await ExternalLoginPictureSync.RefreshGooglePictureIfPresentAsync(
            externalLoginRepository,
            user.Id,
            identity,
            cancellationToken);
        perf.PersistenceMs += persistenceStopwatch.ElapsedMilliseconds;

        return await IssueSessionAsync(user, perf, cancellationToken);
    }

    private async Task<AuthenticationResult> IssueAuthenticationResultAsync(
        User user,
        SocialAuthPerfState perf,
        CancellationToken cancellationToken)
    {
        if (!user.IsActive)
        {
            throw new AuthenticationException("This account is inactive.");
        }

        return await IssueSessionAsync(user, perf, cancellationToken);
    }

    private async Task<AuthenticationResult> IssueSessionAsync(
        User user,
        SocialAuthPerfState perf,
        CancellationToken cancellationToken)
    {
        var issueTokenStopwatch = Stopwatch.StartNew();
        var result = await authenticationSessionService.IssueAsync(user, cancellationToken);
        perf.IssueTokenMs += issueTokenStopwatch.ElapsedMilliseconds;
        return result;
    }

    private static bool ShouldMarkEmailVerified(VerifiedSocialIdentity identity, string email) =>
        identity.IsEmailVerified &&
        !string.IsNullOrWhiteSpace(identity.Email) &&
        !email.EndsWith("@external.movieapp.local", StringComparison.OrdinalIgnoreCase);

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

    private sealed class SocialAuthPerfState
    {
        public long TokenVerificationMs { get; set; }

        public long DbLookupMs { get; set; }

        public long PersistenceMs { get; set; }

        public long IssueTokenMs { get; set; }

        public string? Outcome { get; private set; }

        public void Complete(string outcome) => Outcome = outcome;
    }
}
