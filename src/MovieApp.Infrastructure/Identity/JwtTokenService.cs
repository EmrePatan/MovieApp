using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Text;
using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.Tokens;
using MovieApp.Application.Abstractions.Identity;
using MovieApp.Application.Identity;
using MovieApp.Application.Models.Identity;
using MovieApp.Infrastructure.Configuration;

namespace MovieApp.Infrastructure.Identity;

public sealed class JwtTokenService(IOptions<JwtOptions> options) : ITokenService
{
    public AccessTokenResult CreateAccessToken(TokenUserContext user)
    {
        var jwtOptions = options.Value;
        if (!jwtOptions.IsConfigured())
        {
            throw new InvalidOperationException(
                "JWT signing key is not configured. Provide Authentication:Jwt:SigningKey via user secrets or environment variables.");
        }

        var utcNow = DateTime.UtcNow;
        var expiresAt = utcNow.AddMinutes(jwtOptions.AccessTokenMinutes);
        var signingKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(jwtOptions.SigningKey));
        var credentials = new SigningCredentials(signingKey, SecurityAlgorithms.HmacSha256);

        var claims = new List<Claim>
        {
            new(JwtRegisteredClaimNames.Sub, user.UserId.ToString()),
            new(JwtRegisteredClaimNames.Email, user.Email),
            new(JwtRegisteredClaimNames.Jti, Guid.NewGuid().ToString()),
            new(
                JwtRegisteredClaimNames.Iat,
                EpochTime.GetIntDate(utcNow).ToString(System.Globalization.CultureInfo.InvariantCulture),
                ClaimValueTypes.Integer64),
            new(JwtClaimNames.SecurityStamp, user.SecurityStamp.ToString())
        };

        var token = new JwtSecurityToken(
            issuer: jwtOptions.Issuer,
            audience: jwtOptions.Audience,
            claims: claims,
            notBefore: utcNow,
            expires: expiresAt,
            signingCredentials: credentials);

        return new AccessTokenResult(
            new JwtSecurityTokenHandler().WriteToken(token),
            expiresAt);
    }
}
