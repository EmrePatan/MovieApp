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
    public async Task RegisterAsyncCreatesUnverifiedUserAndSendsVerificationEmailWithoutJwt()
    {
        var repository = new FakeUserRepository(exists: false);
        var resendService = new FakeResendVerificationService();
        var service = new RegisterUserService(
            repository,
            new FakePasswordHasher(),
            resendService);

        var result = await service.RegisterAsync(new RegisterUserRequest(
            " USER@Example.com ",
            "StrongPassword123",
            "Display Name"));

        Assert.True(result.RequiresEmailVerification);
        Assert.Equal(RegisterUserService.VerificationRequiredMessage, result.Message);
        Assert.Equal("USER@Example.com", result.User.Email);
        Assert.Equal(1, repository.CreateCount);
        Assert.Equal(1, resendService.SendCount);
        Assert.False(repository.LastCreatedUser?.IsEmailVerified);
    }

    [Fact]
    public async Task RegisterAsyncThrowsConflictExceptionForDuplicateEmail()
    {
        var service = new RegisterUserService(
            new FakeUserRepository(exists: true),
            new FakePasswordHasher(),
            new FakeResendVerificationService());

        await Assert.ThrowsAsync<ConflictException>(() =>
            service.RegisterAsync(new RegisterUserRequest(
                "user@example.com",
                "StrongPassword123",
                "Display Name")));
    }

    private sealed class FakeUserRepository(bool exists) : IUserRepository
    {
        public int CreateCount { get; private set; }

        public User? LastCreatedUser { get; private set; }

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
            LastCreatedUser = user;
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

    private sealed class FakeResendVerificationService : IResendVerificationService
    {
        public int SendCount { get; private set; }

        public Task<MessageResult> ResendVerificationAsync(
            ResendVerificationRequest request,
            CancellationToken cancellationToken = default) =>
            Task.FromResult(new MessageResult(ResendVerificationService.SuccessMessage));

        public Task SendVerificationEmailAsync(User user, CancellationToken cancellationToken = default)
        {
            SendCount++;
            return Task.CompletedTask;
        }
    }
}
