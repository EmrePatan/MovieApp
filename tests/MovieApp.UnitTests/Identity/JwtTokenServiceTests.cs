using System.IdentityModel.Tokens.Jwt;
using Microsoft.Extensions.Options;
using MovieApp.Application.Identity;
using MovieApp.Application.Models.Identity;
using MovieApp.Infrastructure.Configuration;
using MovieApp.Infrastructure.Identity;

namespace MovieApp.UnitTests.Identity;

public sealed class JwtTokenServiceTests
{
    [Fact]
    public void CreateAccessTokenContainsExpectedClaimsAndExpiration()
    {
        var service = CreateService(accessTokenMinutes: 30);
        var userId = Guid.NewGuid();
        var securityStamp = Guid.NewGuid();

        var token = service.CreateAccessToken(new TokenUserContext(userId, "user@example.com", securityStamp));

        Assert.False(string.IsNullOrWhiteSpace(token.AccessToken));
        Assert.True(token.ExpiresAt > DateTime.UtcNow.AddMinutes(29));

        var handler = new JwtSecurityTokenHandler();
        var jwt = handler.ReadJwtToken(token.AccessToken);

        Assert.Equal(userId.ToString(), jwt.Claims.Single(claim => claim.Type == JwtRegisteredClaimNames.Sub).Value);
        Assert.Equal("user@example.com", jwt.Claims.Single(claim => claim.Type == JwtRegisteredClaimNames.Email).Value);
        Assert.NotNull(jwt.Claims.SingleOrDefault(claim => claim.Type == JwtRegisteredClaimNames.Jti));
        Assert.Equal(
            securityStamp.ToString(),
            jwt.Claims.Single(claim => claim.Type == JwtClaimNames.SecurityStamp).Value);
    }

    [Fact]
    public void CreateAccessTokenThrowsWhenSigningKeyMissing()
    {
        var service = new JwtTokenService(Options.Create(new JwtOptions()));

        Assert.Throws<InvalidOperationException>(() =>
            service.CreateAccessToken(new TokenUserContext(Guid.NewGuid(), "user@example.com", Guid.NewGuid())));
    }

    private static JwtTokenService CreateService(int accessTokenMinutes) =>
        new(Options.Create(new JwtOptions
        {
            Issuer = "MovieApp",
            Audience = "MovieApp.Mobile",
            SigningKey = "unit-test-signing-key-must-be-at-least-32-bytes",
            AccessTokenMinutes = accessTokenMinutes
        }));
}
