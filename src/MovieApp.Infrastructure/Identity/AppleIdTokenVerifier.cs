using System.IdentityModel.Tokens.Jwt;
using System.Linq;
using System.Security.Claims;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.Tokens;
using MovieApp.Application.Abstractions.Identity;
using MovieApp.Application.Exceptions;
using MovieApp.Application.Models.Identity;
using MovieApp.Domain.Users;
using MovieApp.Infrastructure.Configuration;

namespace MovieApp.Infrastructure.Identity;

public sealed class AppleIdTokenVerifier(
    IOptions<SocialAuthOptions> options,
    AppleJwksProvider jwksProvider,
    ILogger<AppleIdTokenVerifier> logger) : ISocialIdentityTokenVerifier
{
    private const string AppleIssuer = "https://appleid.apple.com";

    public string Provider => ExternalLoginProviders.Apple;

    public async Task<VerifiedSocialIdentity> VerifyIdentityTokenAsync(
        string identityToken,
        CancellationToken cancellationToken = default)
    {
        var clientIds = options.Value.Apple.ClientIds
            .Where(clientId => !string.IsNullOrWhiteSpace(clientId))
            .Select(clientId => clientId.Trim())
            .Distinct(StringComparer.Ordinal)
            .ToArray();

        if (clientIds.Length == 0)
        {
            throw new AuthenticationException("Apple sign-in is not configured.");
        }

        var signingKeys = await jwksProvider.GetSigningKeysAsync(cancellationToken);
        var handler = new JwtSecurityTokenHandler
        {
            MapInboundClaims = false,
        };

        ClaimsPrincipal principal;
        try
        {
            principal = handler.ValidateToken(
                identityToken,
                new TokenValidationParameters
                {
                    ValidIssuer = AppleIssuer,
                    ValidAudiences = clientIds,
                    ValidateIssuer = true,
                    ValidateAudience = true,
                    ValidateLifetime = true,
                    ValidateIssuerSigningKey = true,
                    IssuerSigningKeys = signingKeys,
                    ClockSkew = TimeSpan.FromMinutes(1),
                },
                out _);
        }
        catch (SecurityTokenException exception)
        {
            LogValidationFailure(handler, identityToken, clientIds, exception);
            throw new AuthenticationException("Apple identity token is invalid.");
        }

        var subject = principal.FindFirst(JwtRegisteredClaimNames.Sub)?.Value;
        if (string.IsNullOrWhiteSpace(subject))
        {
            throw new AuthenticationException("Apple identity token is invalid.");
        }

        var email = principal.FindFirst(JwtRegisteredClaimNames.Email)?.Value;
        var emailVerifiedClaim = principal.FindFirst("email_verified")?.Value;
        var emailVerified = string.Equals(emailVerifiedClaim, "true", StringComparison.OrdinalIgnoreCase);

        return new VerifiedSocialIdentity(
            Provider,
            subject,
            email,
            emailVerified,
            null);
    }

    private void LogValidationFailure(
        JwtSecurityTokenHandler handler,
        string identityToken,
        string[] configuredAudiences,
        SecurityTokenException exception)
    {
        string tokenIssuer = "unknown";
        string tokenAudiences = "unknown";
        string tokenExpiresAtUtc = "unknown";

        try
        {
            var jwt = handler.ReadJwtToken(identityToken);
            tokenIssuer = jwt.Issuer ?? "unknown";
            tokenAudiences = jwt.Audiences.Any()
                ? string.Join(", ", jwt.Audiences)
                : "none";
            tokenExpiresAtUtc = jwt.ValidTo.ToUniversalTime().ToString("O");
        }
        catch (Exception)
        {
            // Best-effort diagnostics only.
        }

        AppleIdTokenVerifierLogMessages.LogValidationFailed(
            logger,
            exception.GetType().Name,
            string.Join(", ", configuredAudiences),
            tokenIssuer,
            tokenAudiences,
            tokenExpiresAtUtc);
    }
}
