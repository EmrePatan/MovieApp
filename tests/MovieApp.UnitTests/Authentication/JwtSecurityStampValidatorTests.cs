using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.DependencyInjection;
using MovieApp.Api.Authentication;
using MovieApp.Application.Abstractions.Persistence;
using MovieApp.Application.Identity;
using MovieApp.Domain.Entities;
using MovieApp.Infrastructure.Identity;

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

        var authenticateResult = context.Result;
        Assert.NotNull(authenticateResult);
        var failure = authenticateResult.Failure;
        Assert.NotNull(failure);
        Assert.Contains("revoked", failure.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task ValidateAsyncReusesCachedSecurityStampForTheSameUser()
    {
        var userId = Guid.NewGuid();
        var securityStamp = Guid.NewGuid();
        var repository = new CountingUserRepository(userId, securityStamp);
        using var cache = new MemoryCache(new MemoryCacheOptions());
        var stampCache = new SecurityStampCache();
        var httpContext = CreateHttpContext(repository, CancellationToken.None, cache, stampCache);

        await JwtSecurityStampValidator.ValidateAsync(CreateTokenValidatedContext(httpContext, userId, securityStamp));
        await JwtSecurityStampValidator.ValidateAsync(CreateTokenValidatedContext(httpContext, userId, securityStamp));

        Assert.Equal(1, repository.Calls);
    }

    [Fact]
    public async Task ValidateAsyncReadsTheDatabaseAgainAfterTheStampCacheIsInvalidated()
    {
        var userId = Guid.NewGuid();
        var securityStamp = Guid.NewGuid();
        var repository = new CountingUserRepository(userId, securityStamp);
        using var cache = new MemoryCache(new MemoryCacheOptions());
        var stampCache = new SecurityStampCache();
        var httpContext = CreateHttpContext(repository, CancellationToken.None, cache, stampCache);

        await JwtSecurityStampValidator.ValidateAsync(CreateTokenValidatedContext(httpContext, userId, securityStamp));
        SecurityStampCache.Invalidate(cache, userId);
        repository.Stamp = Guid.NewGuid();

        var context = CreateTokenValidatedContext(httpContext, userId, securityStamp);
        await JwtSecurityStampValidator.ValidateAsync(context);

        Assert.Equal(2, repository.Calls);
        Assert.NotNull(context.Result?.Failure);
    }

    private static DefaultHttpContext CreateHttpContext(
        IUserRepository repository,
        CancellationToken requestAborted,
        IMemoryCache? memoryCache = null,
        SecurityStampCache? stampCache = null)
    {
        var services = new ServiceCollection()
            .AddSingleton(repository);
        if (memoryCache is not null)
        {
            services.AddSingleton(memoryCache);
        }

        if (stampCache is not null)
        {
            services.AddSingleton(stampCache);
        }

        var provider = services.BuildServiceProvider();

        return new DefaultHttpContext
        {
            RequestServices = provider,
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

    private sealed class CountingUserRepository(Guid userId, Guid securityStamp) : IUserRepository
    {
        public int Calls { get; private set; }

        public Guid Stamp { get; set; } = securityStamp;

        public Task<Guid?> GetSecurityStampAsync(Guid id, CancellationToken cancellationToken = default)
        {
            Calls++;
            return Task.FromResult<Guid?>(id == userId ? Stamp : null);
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
