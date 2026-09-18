using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Security.Cryptography;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.Tokens;
using MovieApp.Application.Exceptions;
using MovieApp.Infrastructure.Configuration;
using MovieApp.Infrastructure.Identity;

namespace MovieApp.UnitTests.Identity;

public sealed class AppleIdTokenVerifierTests
{
    private const string AppleJwksCacheKey = "apple-signin-jwks";
    private const string BundleAudience = "com.movieapp.mobile";

    [Fact]
    public async Task VerifyIdentityTokenAsyncAcceptsValidNativeIosToken()
    {
        using var rsa = RSA.Create(2048);
        var signingKey = new RsaSecurityKey(rsa.ExportParameters(true));
        var token = CreateAppleIdentityToken(signingKey, BundleAudience, "apple-subject-1");
        var verifier = CreateVerifier(signingKey, BundleAudience);

        var identity = await verifier.VerifyIdentityTokenAsync(token);

        Assert.Equal("apple", identity.Provider);
        Assert.Equal("apple-subject-1", identity.Subject);
        Assert.Equal("apple.user@example.com", identity.Email);
        Assert.True(identity.IsEmailVerified);
    }

    [Fact]
    public async Task VerifyIdentityTokenAsyncRejectsMismatchedAudience()
    {
        using var rsa = RSA.Create(2048);
        var signingKey = new RsaSecurityKey(rsa.ExportParameters(true));
        var token = CreateAppleIdentityToken(signingKey, "com.other.app", "apple-subject-1");
        var verifier = CreateVerifier(signingKey, BundleAudience);

        await Assert.ThrowsAsync<AuthenticationException>(() => verifier.VerifyIdentityTokenAsync(token));
    }

    [Fact]
    public void DefaultInboundClaimMappingHidesSubClaimAfterValidation()
    {
        using var rsa = RSA.Create(2048);
        var signingKey = new RsaSecurityKey(rsa.ExportParameters(true));
        var token = CreateAppleIdentityToken(signingKey, BundleAudience, "apple-subject-1");
        var keySet = CreateJsonWebKeySet(signingKey);

        var defaultHandler = new JwtSecurityTokenHandler();
        var principal = defaultHandler.ValidateToken(
            token,
            new TokenValidationParameters
            {
                ValidIssuer = "https://appleid.apple.com",
                ValidAudiences = [BundleAudience],
                ValidateIssuer = true,
                ValidateAudience = true,
                ValidateLifetime = true,
                ValidateIssuerSigningKey = true,
                IssuerSigningKeys = keySet.GetSigningKeys(),
                ClockSkew = TimeSpan.FromMinutes(1),
            },
            out _);

        Assert.Null(principal.FindFirst(JwtRegisteredClaimNames.Sub)?.Value);
        Assert.Equal("apple-subject-1", principal.FindFirst(ClaimTypes.NameIdentifier)?.Value);
    }

    private static AppleIdTokenVerifier CreateVerifier(SecurityKey signingKey, string audience)
    {
        var options = Options.Create(new SocialAuthOptions
        {
            Apple = new AppleSocialAuthOptions
            {
                ClientIds = [audience],
            },
        });

        var memoryCache = new MemoryCache(new MemoryCacheOptions());
        memoryCache.Set(AppleJwksCacheKey, CreateJsonWebKeySet(signingKey), TimeSpan.FromHours(12));

        var jwksProvider = new AppleJwksProvider(
            new TestHttpClientFactory(),
            memoryCache,
            NullLogger<AppleJwksProvider>.Instance);
        return new AppleIdTokenVerifier(options, jwksProvider, NullLogger<AppleIdTokenVerifier>.Instance);
    }

    private static JsonWebKeySet CreateJsonWebKeySet(SecurityKey signingKey)
    {
        var jwk = JsonWebKeyConverter.ConvertFromRSASecurityKey(signingKey as RsaSecurityKey);
        var keySet = new JsonWebKeySet();
        keySet.Keys.Add(jwk);
        return keySet;
    }

    private static string CreateAppleIdentityToken(SecurityKey signingKey, string audience, string subject)
    {
        var credentials = new SigningCredentials(signingKey, SecurityAlgorithms.RsaSha256);
        var token = new JwtSecurityToken(
            issuer: "https://appleid.apple.com",
            audience: audience,
            claims:
            [
                new Claim(JwtRegisteredClaimNames.Sub, subject),
                new Claim(JwtRegisteredClaimNames.Email, "apple.user@example.com"),
                new Claim("email_verified", "true"),
            ],
            notBefore: DateTime.UtcNow.AddMinutes(-1),
            expires: DateTime.UtcNow.AddMinutes(10),
            signingCredentials: credentials);

        return new JwtSecurityTokenHandler().WriteToken(token);
    }

    private sealed class TestHttpClientFactory : IHttpClientFactory
    {
        public HttpClient CreateClient(string name) => new();
    }
}
