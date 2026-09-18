using System.Diagnostics;
using Google.Apis.Auth;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using MovieApp.Application.Abstractions.Identity;
using MovieApp.Application.Exceptions;
using MovieApp.Application.Models.Identity;
using MovieApp.Domain.Users;
using MovieApp.Infrastructure.Configuration;

namespace MovieApp.Infrastructure.Identity;

public sealed class GoogleIdTokenVerifier(
    IOptions<SocialAuthOptions> options,
    ILogger<GoogleIdTokenVerifier> logger) : ISocialIdentityTokenVerifier
{
    public string Provider => ExternalLoginProviders.Google;

    public async Task<VerifiedSocialIdentity> VerifyIdentityTokenAsync(
        string identityToken,
        CancellationToken cancellationToken = default)
    {
        var clientIds = options.Value.Google.ClientIds
            .Where(clientId => !string.IsNullOrWhiteSpace(clientId))
            .Select(clientId => clientId.Trim())
            .Distinct(StringComparer.Ordinal)
            .ToArray();

        if (clientIds.Length == 0)
        {
            throw new AuthenticationException("Google sign-in is not configured.");
        }

        GoogleJsonWebSignature.Payload payload;
        var validationStopwatch = Stopwatch.StartNew();
        try
        {
            payload = await GoogleJsonWebSignature.ValidateAsync(
                identityToken,
                new GoogleJsonWebSignature.ValidationSettings
                {
                    Audience = clientIds,
                });
        }
        catch (InvalidJwtException)
        {
            throw new AuthenticationException("Google identity token is invalid.");
        }
        finally
        {
            GoogleIdTokenVerifierLogMessages.LogTokenValidation(logger, validationStopwatch.ElapsedMilliseconds);
        }

        if (string.IsNullOrWhiteSpace(payload.Subject))
        {
            throw new AuthenticationException("Google identity token is invalid.");
        }

        var emailVerified = payload.EmailVerified == true;

        return new VerifiedSocialIdentity(
            Provider,
            payload.Subject,
            payload.Email,
            emailVerified,
            payload.Name);
    }
}
