using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.DependencyInjection;
using MovieApp.Api.Authentication;
using MovieApp.Application.Abstractions.Persistence;
using MovieApp.Application.Identity;
using MovieApp.Domain.Entities;

namespace MovieApp.UnitTests.Authentication;

public sealed class JwtSecurityStampValidatorTests
{
    [Fact]
    public async Task ValidateAsyncPassesRequestAbortedTokenToRepository()
    {
        var userId = Guid.NewGuid();
        var securityStamp = Guid.NewGuid();
        using var cancellation = new CancellationTokenSource();

        CancellationToken capturedToken = default;
        var repository = new CapturingUserRepository(
            userId,
            securityStamp,
            token => capturedToken = token);

        var httpContext = CreateHttpContext(repository, cancellation.Token);
        var context = CreateTokenValidatedContext(httpContext, userId, securityStamp);

        await JwtSecurityStampValidator.ValidateAsync(context);

        Assert.Equal(cancellation.Token, capturedToken);
    }

    [Fact]
    public async Task ValidateAsyncPropagatesCancellationWhenRequestIsAborted()
    {
        var userId = Guid.NewGuid();
        var securityStamp = Guid.NewGuid();
        using var cancellation = new CancellationTokenSource();
        cancellation.Cancel();

        var repository = new CancelingUserRepository();
        var httpContext = CreateHttpContext(repository, cancellation.Token);
        var context = CreateTokenValidatedContext(httpContext, userId, securityStamp);

        await Assert.ThrowsAnyAsync<OperationCanceledException>(
            () => JwtSecurityStampValidator.ValidateAsync(context));
    }

    [Fact]
    public async Task ValidateAsyncFailsWhenSecurityStampDoesNotMatch()
    {
        var userId = Guid.NewGuid();
        var securityStamp = Guid.NewGuid();
        var repository = new CapturingUserRepository(userId, Guid.NewGuid(), _ => { });
        var httpContext = CreateHttpContext(repository, CancellationToken.None);
        var context = CreateTokenValidatedContext(httpContext, userId, securityStamp);

        await JwtSecurityStampValidator.ValidateAsync(context);

        Assert.NotNull(context.Result.Failure);
        Assert.Contains("revoked", context.Result.Failure.Message, StringComparison.OrdinalIgnoreCase);
    }

    private static DefaultHttpContext CreateHttpContext(
        IUserRepository repository,
        CancellationToken requestAborted)
    {
        var services = new ServiceCollection()
            .AddSingleton(repository)
            .BuildServiceProvider();

        return new DefaultHttpContext
        {
            RequestServices = services,
            RequestAborted = requestAborted,
        };
    }

    private static TokenValidatedContext CreateTokenValidatedContext(
        HttpContext httpContext,
        Guid userId,
        Guid securityStamp)
    {
        var identity = new ClaimsIdentity(
        [
            new Claim(ClaimTypes.NameIdentifier, userId.ToString()),
            new Claim(JwtClaimNames.SecurityStamp, securityStamp.ToString()),
        ],
        JwtBearerDefaults.AuthenticationScheme);

        var context = new TokenValidatedContext(
            httpContext,
            new AuthenticationScheme(
                JwtBearerDefaults.AuthenticationScheme,
                null,
                typeof(JwtBearerHandler)),
            new JwtBearerOptions())
        {
            Principal = new ClaimsPrincipal(identity),
        };

        return context;
    }

    private sealed class CapturingUserRepository(
        Guid userId,
        Guid securityStamp,
        Action<CancellationToken> captureToken) : IUserRepository
    {
        public Task<Guid?> GetSecurityStampAsync(Guid id, CancellationToken cancellationToken = default)
        {
            captureToken(cancellationToken);
            return Task.FromResult<Guid?>(id == userId ? securityStamp : null);
        }

        public Task<User?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default) =>
            throw new NotSupportedException();

        public Task<User?> GetByIdForUpdateAsync(Guid id, CancellationToken cancellationToken = default) =>
            throw new NotSupportedException();

        public Task<User?> GetByNormalizedEmailAsync(string normalizedEmail, CancellationToken cancellationToken = default) =>
            throw new NotSupportedException();

        public Task<bool> ExistsByNormalizedEmailAsync(string normalizedEmail, CancellationToken cancellationToken = default) =>
            throw new NotSupportedException();

        public Task<User> CreateAsync(User user, CancellationToken cancellationToken = default) =>
            throw new NotSupportedException();

        public Task UpdateAsync(User user, CancellationToken cancellationToken = default) =>
            throw new NotSupportedException();

        public Task<bool> DeleteAsync(Guid id, CancellationToken cancellationToken = default) =>
            throw new NotSupportedException();
    }

    private sealed class CancelingUserRepository : IUserRepository
    {
        public Task<Guid?> GetSecurityStampAsync(Guid id, CancellationToken cancellationToken = default) =>
            Task.FromCanceled<Guid?>(cancellationToken);

        public Task<User?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default) =>
            throw new NotSupportedException();

        public Task<User?> GetByIdForUpdateAsync(Guid id, CancellationToken cancellationToken = default) =>
            throw new NotSupportedException();

        public Task<User?> GetByNormalizedEmailAsync(string normalizedEmail, CancellationToken cancellationToken = default) =>
            throw new NotSupportedException();

        public Task<bool> ExistsByNormalizedEmailAsync(string normalizedEmail, CancellationToken cancellationToken = default) =>
            throw new NotSupportedException();

        public Task<User> CreateAsync(User user, CancellationToken cancellationToken = default) =>
            throw new NotSupportedException();

        public Task UpdateAsync(User user, CancellationToken cancellationToken = default) =>
            throw new NotSupportedException();

        public Task<bool> DeleteAsync(Guid id, CancellationToken cancellationToken = default) =>
            throw new NotSupportedException();
    }
}
