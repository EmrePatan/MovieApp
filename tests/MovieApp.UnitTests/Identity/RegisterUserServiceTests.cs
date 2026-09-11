using MovieApp.Application.Abstractions.Identity;
using MovieApp.Application.Abstractions.Persistence;
using MovieApp.Application.Exceptions;
using MovieApp.Application.Models.Identity;
using MovieApp.Application.Services.Identity;
using MovieApp.Domain.Entities;
using MovieApp.Domain.Users;

namespace MovieApp.UnitTests.Identity;

public sealed class RegisterUserServiceTests
{
    [Fact]
    public async Task RegisterAsyncCreatesUserAndReturnsToken()
    {
        var repository = new FakeUserRepository(exists: false);
        var service = new RegisterUserService(
            repository,
            new FakePasswordHasher(),
            new FakeTokenService());

        var result = await service.RegisterAsync(new RegisterUserRequest(
            " USER@Example.com ",
            "StrongPassword123",
            "Display Name"));

        Assert.Equal("token", result.AccessToken);
        Assert.Equal("USER@Example.com", result.User.Email);
        Assert.Equal(1, repository.CreateCount);
    }

    [Fact]
    public async Task RegisterAsyncThrowsConflictExceptionForDuplicateEmail()
    {
        var service = new RegisterUserService(
            new FakeUserRepository(exists: true),
            new FakePasswordHasher(),
            new FakeTokenService());

        await Assert.ThrowsAsync<ConflictException>(() =>
            service.RegisterAsync(new RegisterUserRequest(
                "user@example.com",
                "StrongPassword123",
                "Display Name")));
    }

    private sealed class FakeUserRepository(bool exists) : IUserRepository
    {
        public int CreateCount { get; private set; }

        public Task<User?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default) =>
            Task.FromResult<User?>(null);

        public Task<User?> GetByIdForUpdateAsync(Guid id, CancellationToken cancellationToken = default) =>
            Task.FromResult<User?>(null);

        public Task<Guid?> GetSecurityStampAsync(Guid id, CancellationToken cancellationToken = default) =>
            Task.FromResult<Guid?>(null);

        public Task<User?> GetByNormalizedEmailAsync(string normalizedEmail, CancellationToken cancellationToken = default) =>
            Task.FromResult<User?>(null);

        public Task<bool> ExistsByNormalizedEmailAsync(string normalizedEmail, CancellationToken cancellationToken = default) =>
            Task.FromResult(exists);

        public Task<User> CreateAsync(User user, CancellationToken cancellationToken = default)
        {
            CreateCount++;
            return Task.FromResult(user);
        }

        public Task UpdateAsync(User user, CancellationToken cancellationToken = default) =>
            Task.CompletedTask;

        public Task<bool> DeleteAsync(Guid id, CancellationToken cancellationToken = default) =>
            Task.FromResult(false);
    }

    private sealed class FakePasswordHasher : IPasswordHasher
    {
        public string HashPassword(string password) => "hashed-password";

        public bool VerifyPassword(string password, string passwordHash) => true;
    }

    private sealed class FakeTokenService : ITokenService
    {
        public AccessTokenResult CreateAccessToken(TokenUserContext user) =>
            new("token", DateTime.UtcNow.AddHours(1));
    }
}
