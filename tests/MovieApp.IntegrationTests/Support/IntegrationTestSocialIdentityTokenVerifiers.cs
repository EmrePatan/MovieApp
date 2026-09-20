using MovieApp.Application.Abstractions.Identity;
using MovieApp.Application.Exceptions;
using MovieApp.Application.Models.Identity;
using MovieApp.Domain.Users;

namespace MovieApp.IntegrationTests.Support;

public sealed class IntegrationTestGoogleIdentityTokenVerifier : ISocialIdentityTokenVerifier
{
    public const string ValidToken = "integration-test-google-token";
    public const string Subject = "integration-google-subject";
    public const string Email = "social-google@example.com";

    public string Provider => ExternalLoginProviders.Google;

    public Task<VerifiedSocialIdentity> VerifyIdentityTokenAsync(
        string identityToken,
        CancellationToken cancellationToken = default)
    {
        if (!string.Equals(identityToken, ValidToken, StringComparison.Ordinal))
        {
            throw new AuthenticationException("Invalid token.");
        }

        return Task.FromResult(new VerifiedSocialIdentity(
            ExternalLoginProviders.Google,
            Subject,
            Email,
            true,
            "Social Google User"));
    }
}

public sealed class IntegrationTestAppleIdentityTokenVerifier : ISocialIdentityTokenVerifier
{
    public const string ValidToken = "integration-test-apple-token";
    public const string Subject = "integration-apple-subject";

    public string Provider => ExternalLoginProviders.Apple;

    public Task<VerifiedSocialIdentity> VerifyIdentityTokenAsync(
        string identityToken,
        CancellationToken cancellationToken = default)
    {
        if (!string.Equals(identityToken, ValidToken, StringComparison.Ordinal))
        {
            throw new AuthenticationException("Invalid token.");
        }

        return Task.FromResult(new VerifiedSocialIdentity(
            ExternalLoginProviders.Apple,
            Subject,
            "social-apple@example.com",
            true,
            "Social Apple User"));
    }
}
