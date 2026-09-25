using Microsoft.Extensions.Options;
using MovieApp.Application.Abstractions.Identity;
using MovieApp.Application.Abstractions.Persistence;
using MovieApp.Application.Configuration;
using MovieApp.Application.Exceptions;
using MovieApp.Application.Identity;
using MovieApp.Application.Mapping;
using MovieApp.Application.Models.Identity;
using MovieApp.Domain.Entities;

namespace MovieApp.Application.Services.Identity;

public sealed class AuthenticationSessionService(
    ITokenService tokenService,
    IRefreshTokenRepository refreshTokenRepository,
    IUserRepository userRepository,
    IApplicationDbContext applicationDbContext,
    IOptions<RefreshTokenOptions> refreshTokenOptions) : IAuthenticationSessionService
{
    private const string InvalidRefreshTokenMessage = "Invalid refresh token.";

    public async Task<AuthenticationResult> IssueAsync(User user, CancellationToken cancellationToken = default)
    {
        var accessToken = tokenService.CreateAccessToken(UserMapper.ToTokenUserContext(user));
        var refresh = await CreateRefreshTokenAsync(user.Id, cancellationToken);

        return new AuthenticationResult(
            accessToken.AccessToken,
            accessToken.ExpiresAt,
            UserMapper.ToCurrentUserResult(user),
            refresh.RawToken,
            refresh.ExpiresAtUtc);
    }

    public async Task<AuthenticationResult> RefreshAsync(string refreshToken, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(refreshToken))
        {
            throw new AuthenticationException(InvalidRefreshTokenMessage);
        }

        var utcNow = DateTime.UtcNow;
        var tokenHash = PasswordResetTokenHasher.HashToken(refreshToken.Trim());
        AuthenticationResult? result = null;

        await applicationDbContext.ExecuteInTransactionAsync(async innerCancellationToken =>
        {
            var userId = await refreshTokenRepository.TryRevokeActiveTokenAsync(
                tokenHash,
                utcNow,
                innerCancellationToken);

            if (userId is null)
            {
                throw new AuthenticationException(InvalidRefreshTokenMessage);
            }

            var user = await userRepository.GetByIdAsync(userId.Value, innerCancellationToken);
            if (user is null || !user.IsActive || !user.IsEmailVerified)
            {
                throw new AuthenticationException(InvalidRefreshTokenMessage);
            }

            var accessToken = tokenService.CreateAccessToken(UserMapper.ToTokenUserContext(user));
            var refresh = await CreateRefreshTokenAsync(user.Id, innerCancellationToken);

            result = new AuthenticationResult(
                accessToken.AccessToken,
                accessToken.ExpiresAt,
                UserMapper.ToCurrentUserResult(user),
                refresh.RawToken,
                refresh.ExpiresAtUtc);
        }, cancellationToken);

        return result!;
    }

    public async Task RevokeRefreshTokenAsync(string refreshToken, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(refreshToken))
        {
            return;
        }

        var utcNow = DateTime.UtcNow;
        var tokenHash = PasswordResetTokenHasher.HashToken(refreshToken.Trim());
        await refreshTokenRepository.TryRevokeActiveTokenAsync(tokenHash, utcNow, cancellationToken);
    }

    public Task RevokeAllRefreshTokensForUserAsync(Guid userId, CancellationToken cancellationToken = default)
    {
        return refreshTokenRepository.RevokeAllActiveForUserAsync(userId, DateTime.UtcNow, cancellationToken);
    }

    private async Task<(string RawToken, DateTime ExpiresAtUtc)> CreateRefreshTokenAsync(
        Guid userId,
        CancellationToken cancellationToken)
    {
        var utcNow = DateTime.UtcNow;
        var lifetimeDays = Math.Max(1, refreshTokenOptions.Value.LifetimeDays);
        var expiresAtUtc = utcNow.AddDays(lifetimeDays);
        var rawToken = PasswordResetTokenGenerator.GenerateToken();

        var entity = new RefreshToken
        {
            Id = Guid.NewGuid(),
            UserId = userId,
            TokenHash = PasswordResetTokenHasher.HashToken(rawToken),
            CreatedAtUtc = utcNow,
            ExpiresAtUtc = expiresAtUtc
        };

        await refreshTokenRepository.CreateAsync(entity, cancellationToken);
        return (rawToken, expiresAtUtc);
    }
}
