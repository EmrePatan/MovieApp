using MovieApp.Application.Mapping;
using MovieApp.Application.Models.Identity;
using MovieApp.Application.Services.Identity;
using MovieApp.Domain.Entities;

namespace MovieApp.UnitTests.Identity;

internal sealed class FakeAuthenticationSessionService : IAuthenticationSessionService
{
    public Task<AuthenticationResult> IssueAsync(User user, CancellationToken cancellationToken = default) =>
        Task.FromResult(new AuthenticationResult(
            "token",
            DateTime.UtcNow.AddHours(1),
            UserMapper.ToCurrentUserResult(user),
            "refresh-token",
            DateTime.UtcNow.AddDays(30)));

    public Task<AuthenticationResult> RefreshAsync(string refreshToken, CancellationToken cancellationToken = default) =>
        throw new NotSupportedException();

    public Task RevokeRefreshTokenAsync(string refreshToken, CancellationToken cancellationToken = default) =>
        Task.CompletedTask;

    public Task RevokeAllRefreshTokensForUserAsync(Guid userId, CancellationToken cancellationToken = default) =>
        Task.CompletedTask;
}
