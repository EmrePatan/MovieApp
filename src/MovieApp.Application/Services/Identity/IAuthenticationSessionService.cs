using MovieApp.Application.Models.Identity;
using MovieApp.Domain.Entities;

namespace MovieApp.Application.Services.Identity;

public interface IAuthenticationSessionService
{
    Task<AuthenticationResult> IssueAsync(User user, CancellationToken cancellationToken = default);

    Task<AuthenticationResult> RefreshAsync(string refreshToken, CancellationToken cancellationToken = default);

    Task RevokeRefreshTokenAsync(string refreshToken, CancellationToken cancellationToken = default);

    Task RevokeAllRefreshTokensForUserAsync(Guid userId, CancellationToken cancellationToken = default);
}
