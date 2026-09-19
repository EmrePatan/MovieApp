using MovieApp.Application.Abstractions.Identity;
using MovieApp.Application.Abstractions.Persistence;
using MovieApp.Application.Exceptions;
using MovieApp.Application.Models.Identity;
using MovieApp.Application.Services.Identity;
using MovieApp.Domain.Entities;

namespace MovieApp.UnitTests.Identity;

public sealed class VerifyEmailServiceTests
{
    [Fact]
    public async Task VerifyEmailAsyncMarksUserVerifiedAndReturnsJwt()
    {
        var user = User.Create(
            Guid.NewGuid(),
            "user@example.com",
            "hashed-password",
            "Display Name",
            DateTime.UtcNow);
        var repository = new FakeUserRepository(user);
        var tokenRepository = new FakeEmailVerificationTokenRepository(
            new EmailVerificationTokenConsumptionResult(Guid.NewGuid(), user.Id));
        var service = CreateService(repository, tokenRepository);

        var result = await service.VerifyEmailAsync(new VerifyEmailRequest("raw-token"));

        Assert.Equal("token", result.AccessToken);
        Assert.True(user.IsEmailVerified);
        Assert.NotNull(user.LastLoginAt);
        Assert.Equal(1, repository.UpdateCount);
    }

    [Fact]
    public async Task VerifyEmailAsyncThrowsValidationExceptionForInvalidToken()
    {
        var service = CreateService(
            new FakeUserRepository(null),
            new FakeEmailVerificationTokenRepository(null));

        await Assert.ThrowsAsync<ValidationException>(() =>
            service.VerifyEmailAsync(new VerifyEmailRequest("invalid-token")));
    }

    [Fact]
    public async Task VerifyEmailAsyncThrowsValidationExceptionForEmptyToken()
    {
        var service = CreateService(
            new FakeUserRepository(null),
            new FakeEmailVerificationTokenRepository(null));

        await Assert.ThrowsAsync<ValidationException>(() =>
            service.VerifyEmailAsync(new VerifyEmailRequest("")));
    }

    private static VerifyEmailService CreateService(
        FakeUserRepository userRepository,
        FakeEmailVerificationTokenRepository tokenRepository) =>
        new(
            new FakeApplicationDbContext(),
            userRepository,
            tokenRepository,
            new FakeTokenService());

    private sealed class FakeApplicationDbContext : IApplicationDbContext
    {
        public Task<int> SaveChangesAsync(CancellationToken cancellationToken = default) =>
            Task.FromResult(0);

        public Task ExecuteInTransactionAsync(
            Func<CancellationToken, Task> action,
            CancellationToken cancellationToken = default) =>
            action(cancellationToken);
    }

    private sealed class FakeUserRepository(User? user) : IUserRepository
    {
        public int UpdateCount { get; private set; }

        public Task<User?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default) =>
            Task.FromResult(user);

        public Task<User?> GetByIdForUpdateAsync(Guid id, CancellationToken cancellationToken = default) =>
            Task.FromResult(user is not null && user.Id == id ? user : null);

        public Task<Guid?> GetSecurityStampAsync(Guid id, CancellationToken cancellationToken = default) =>
            Task.FromResult<Guid?>(user?.SecurityStamp);

        public Task<User?> GetByNormalizedEmailAsync(string normalizedEmail, CancellationToken cancellationToken = default) =>
            Task.FromResult<User?>(null);

        public Task<bool> ExistsByNormalizedEmailAsync(string normalizedEmail, CancellationToken cancellationToken = default) =>
            Task.FromResult(false);

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

    private sealed class FakeEmailVerificationTokenRepository(
        EmailVerificationTokenConsumptionResult? consumptionResult) : IEmailVerificationTokenRepository
    {
        public Task<EmailVerificationTokenConsumptionResult?> TryConsumeActiveTokenAsync(
            string tokenHash,
            DateTime utcNow,
            CancellationToken cancellationToken = default) =>
            Task.FromResult(consumptionResult);

        public Task CreateAsync(EmailVerificationToken token, CancellationToken cancellationToken = default) =>
            Task.CompletedTask;

        public Task InvalidateActiveTokensForUserAsync(
            Guid userId,
            DateTime utcNow,
            CancellationToken cancellationToken = default) =>
            Task.CompletedTask;
    }

    private sealed class FakeTokenService : ITokenService
    {
        public AccessTokenResult CreateAccessToken(TokenUserContext user) =>
            new("token", DateTime.UtcNow.AddHours(1));
    }
}
