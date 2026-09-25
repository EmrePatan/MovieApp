using MovieApp.Application.Abstractions.Identity;
using MovieApp.Application.Abstractions.Persistence;
using MovieApp.Application.Exceptions;
using MovieApp.Application.Models.Identity;
using MovieApp.Application.Services.Identity;
using MovieApp.Domain.Entities;
using MovieApp.Domain.Users;

namespace MovieApp.UnitTests.Identity;

public sealed class LoginUserServiceTests
{
    [Fact]
    public async Task LoginAsyncReturnsTokenAndUpdatesLastLoginAt()
    {
        var user = CreateUser();
        user.MarkEmailVerified(DateTime.UtcNow);
        var repository = new FakeUserRepository(user);
        var passwordHasher = new FakePasswordHasher(shouldVerify: true);
        var service = new LoginUserService(repository, passwordHasher, new FakeAuthenticationSessionService());

        var result = await service.LoginAsync(new LoginUserRequest("user@example.com", "StrongPassword123"));

        Assert.Equal("token", result.AccessToken);
        Assert.NotNull(user.LastLoginAt);
        Assert.Equal(1, repository.UpdateCount);
    }

    [Fact]
    public async Task LoginAsyncThrowsAuthenticationExceptionForInvalidCredentials()
    {
        var repository = new FakeUserRepository(null);
        var service = new LoginUserService(repository, new FakePasswordHasher(true), new FakeAuthenticationSessionService());

        await Assert.ThrowsAsync<AuthenticationException>(() =>
            service.LoginAsync(new LoginUserRequest("missing@example.com", "StrongPassword123")));
    }

    [Fact]
    public async Task LoginAsyncThrowsAuthenticationExceptionForSocialOnlyUser()
    {
        var user = User.CreateFromExternalIdentity(
            Guid.NewGuid(),
            "social@example.com",
            "Social User",
            DateTime.UtcNow);

        var service = new LoginUserService(
            new FakeUserRepository(user),
            new FakePasswordHasher(true),
            new FakeAuthenticationSessionService());

        await Assert.ThrowsAsync<AuthenticationException>(() =>
            service.LoginAsync(new LoginUserRequest("social@example.com", "StrongPassword123")));
    }

    [Fact]
    public async Task LoginAsyncThrowsEmailNotVerifiedExceptionForUnverifiedPasswordUser()
    {
        var user = CreateUser();
        var service = new LoginUserService(
            new FakeUserRepository(user),
            new FakePasswordHasher(true),
            new FakeAuthenticationSessionService());

        await Assert.ThrowsAsync<EmailNotVerifiedException>(() =>
            service.LoginAsync(new LoginUserRequest("user@example.com", "StrongPassword123")));
    }

    [Fact]
    public async Task LoginAsyncReturnsTokenForVerifiedPasswordUser()
    {
        var user = CreateUser();
        user.MarkEmailVerified(DateTime.UtcNow);
        var service = new LoginUserService(
            new FakeUserRepository(user),
            new FakePasswordHasher(true),
            new FakeAuthenticationSessionService());

        var result = await service.LoginAsync(new LoginUserRequest("user@example.com", "StrongPassword123"));

        Assert.Equal("token", result.AccessToken);
    }

    [Fact]
    public async Task LoginAsyncThrowsAuthenticationExceptionForInactiveUser()
    {
        var user = CreateUser();
        user.IsActive = false;
        var service = new LoginUserService(
            new FakeUserRepository(user),
            new FakePasswordHasher(true),
            new FakeAuthenticationSessionService());

        await Assert.ThrowsAsync<AuthenticationException>(() =>
            service.LoginAsync(new LoginUserRequest("user@example.com", "StrongPassword123")));
    }

    private static User CreateUser() =>
        User.Create(
            Guid.NewGuid(),
            "user@example.com",
            "hashed-password",
            "Display Name",
            DateTime.UtcNow);

    private sealed class FakeUserRepository(User? user) : IUserRepository
    {
        public int UpdateCount { get; private set; }

        public Task<User?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default) =>
            Task.FromResult(user);

        public Task<User?> GetByIdForUpdateAsync(Guid id, CancellationToken cancellationToken = default) =>
            Task.FromResult(user);

        public Task<Guid?> GetSecurityStampAsync(Guid id, CancellationToken cancellationToken = default) =>
            Task.FromResult(user?.SecurityStamp);

        public Task<User?> GetByNormalizedEmailAsync(string normalizedEmail, CancellationToken cancellationToken = default) =>
            Task.FromResult(user is not null && user.NormalizedEmail == normalizedEmail ? user : null);

        public Task<bool> ExistsByNormalizedEmailAsync(string normalizedEmail, CancellationToken cancellationToken = default) =>
            Task.FromResult(user is not null && user.NormalizedEmail == normalizedEmail);

        public Task<User> CreateAsync(User user, CancellationToken cancellationToken = default) =>
            Task.FromResult(user);

        public Task UpdateAsync(User user, CancellationToken cancellationToken = default)
        {
            UpdateCount++;
            return Task.CompletedTask;
        }

        public Task<bool> DeleteAsync(Guid id, CancellationToken cancellationToken = default) =>
            Task.FromResult(false);
    }

    private sealed class FakePasswordHasher(bool shouldVerify) : IPasswordHasher
    {
        public string HashPassword(string password) => "hashed-password";

        public bool VerifyPassword(string password, string passwordHash) => shouldVerify;
    }

}
